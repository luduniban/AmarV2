using AmarV.Core.Entrada;
using AmarV.Core.Resultado;

namespace AmarV.Core.Analise;

/// <summary>
/// Uma "série" é um esquema vacinal completo dentro das regras cadastradas.
/// Ex.: Pentavalente (dose 1 aos 2 m, dose 2 aos 4 m, dose 3 aos 6 m).
///
/// No banco, cada dose é uma regra. A mesma vacina pode ter várias séries
/// (rotina da criança, recuperação de 7 a 9 anos, adulto, gestante...).
/// As regras de uma série ficam em sequência no cadastro; quando o número
/// da dose "volta" (ex.: 3 → 1), começa outra série.
/// </summary>
public sealed class Serie
{
    public string Chave { get; init; } = string.Empty;
    public string NomeExibicao { get; init; } = string.Empty;
    public string Calendario { get; init; } = Calendarios.SUS;
    public string GrupoEtario { get; init; } = string.Empty;
    public string GrupoEspecial { get; init; } = string.Empty;
    public List<RegraCalendario> Regras { get; } = new();

    public int Total => Math.Max(Regras.Max(r => r.TotalDoses), Regras.Max(r => r.NumeroDose));
    public int IdadeInicio => Regras.Min(r => r.IdadeMinimaMeses);
    public int IdadeFim => Regras.Max(r => r.IdadeMaximaEfetiva);
    public bool EhGestante => CatalogoVacinas.EhGrupoGestante(GrupoEtario);
    public bool EhPuerpera => Texto.Normalizar(GrupoEspecial).Contains("puerpera");
    public bool EhEspecial => !CatalogoVacinas.EhGrupoGeral(GrupoEspecial);
    public int SemanaGestacionalMinima => Regras.Max(r => r.SemanaGestacionalMinima);

    public string Periodicidade =>
        Regras.OrderByDescending(r => r.NumeroDose)
              .Select(r => r.Periodicidade)
              .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;

    public RegraCalendario? Dose(int numero) => Regras.FirstOrDefault(r => r.NumeroDose == numero);

    public override string ToString() =>
        $"{NomeExibicao} [{Calendario}/{GrupoEtario}/{GrupoEspecial}] {IdadeInicio}-{IdadeFim} m, {Total} dose(s)";
}

/// <summary>Esquema efetivo de uma vacina para um paciente (séries aplicáveis combinadas).</summary>
public sealed class EsquemaPaciente
{
    public string Chave { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string Calendario { get; init; } = Calendarios.SUS;
    public List<Serie> Series { get; init; } = new();
    public int Total { get; set; }
    public bool Futuro { get; init; }
    public bool Expirado { get; init; }

    /// <summary>Esquema de dose única que vale por gestação (dTpa, VSR, COVID na gestante).</summary>
    public bool PorGestacao { get; init; }
    public int SemanaGestacionalMinima { get; init; }

    /// <summary>Esquema de dose única que vale pelo puerpério (dTpa puérpera).</summary>
    public bool PorPuerperio { get; init; }

    public string Periodicidade =>
        Series.Select(s => s.Periodicidade).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;

    /// <summary>Regra da dose n; prioriza gestante, depois grupo especial, depois a série geral.</summary>
    public RegraCalendario? Dose(int numero) =>
        Series.OrderByDescending(s => s.EhGestante)
              .ThenByDescending(s => s.EhEspecial)
              .ThenBy(s => s.IdadeInicio)
              .Select(s => s.Dose(numero))
              .FirstOrDefault(r => r != null);

    public RegraCalendario UltimaRegra =>
        Series.SelectMany(s => s.Regras).OrderByDescending(r => r.NumeroDose).First();

    public RegraCalendario PrimeiraRegra =>
        Dose(1) ?? Series.SelectMany(s => s.Regras).OrderBy(r => r.NumeroDose).First();
}

public static class MontadorEsquemas
{
    /// <summary>Agrupa as regras cadastradas em séries.</summary>
    public static List<Serie> MontarSeries(IEnumerable<RegraCalendario> regras)
    {
        var series = new List<Serie>();

        var grupos = regras
            .Where(r => !string.IsNullOrWhiteSpace(r.NomeVacina) && r.NumeroDose > 0)
            .GroupBy(r => (
                Chave: CatalogoVacinas.Chave(r.NomeVacina),
                Cal: CatalogoVacinas.Calendario(r.TipoCalendario),
                Grupo: Texto.Normalizar(r.GrupoEtario),
                Especial: CatalogoVacinas.EhGrupoGeral(r.GrupoEspecial) ? "" : Texto.Normalizar(r.GrupoEspecial)));

        foreach (var g in grupos)
        {
            Serie? atual = null;
            int ultimaDose = 0;

            foreach (var r in g.OrderBy(r => r.Id))
            {
                if (atual == null || r.NumeroDose <= ultimaDose)
                {
                    atual = new Serie
                    {
                        Chave = g.Key.Chave,
                        NomeExibicao = r.NomeVacina.Trim(),
                        Calendario = g.Key.Cal,
                        GrupoEtario = r.GrupoEtario ?? string.Empty,
                        GrupoEspecial = r.GrupoEspecial ?? string.Empty
                    };
                    series.Add(atual);
                }
                atual.Regras.Add(r);
                ultimaDose = r.NumeroDose;
            }
        }

        return series;
    }

    /// <summary>
    /// Escolhe, para cada vacina, as séries que valem para o paciente hoje.
    /// Se nenhuma vale hoje, usa a próxima série futura (dentro do horizonte).
    /// Se todas já passaram, devolve um esquema "expirado" (só para crianças).
    /// </summary>
    public static List<EsquemaPaciente> Selecionar(
        List<Serie> series, PerfilPaciente p, int idadeMeses, ParametrosAnalise prm)
    {
        var resultado = new List<EsquemaPaciente>();
        DateTime limiteFuturo = prm.DataReferencia.AddMonths(prm.HorizonteMeses);

        foreach (var porVacina in series.GroupBy(s => (s.Chave, s.Calendario)))
        {
            var elegiveis = porVacina.Where(s =>
                    CatalogoVacinas.AtendeGrupoEspecial(s.GrupoEspecial, p) &&
                    (!s.EhGestante || p.Gestante))
                .ToList();
            if (elegiveis.Count == 0) continue;

            var atuais = elegiveis.Where(s => idadeMeses >= s.IdadeInicio && idadeMeses <= s.IdadeFim).ToList();

            List<Serie> escolhidas;
            bool futuro = false, expirado = false;

            if (atuais.Count > 0)
            {
                escolhidas = atuais;
            }
            else
            {
                var futuras = elegiveis
                    .Where(s => s.IdadeInicio > idadeMeses &&
                                Datas.DataNaIdade(p.DataNascimento, s.IdadeInicio) <= limiteFuturo)
                    .ToList();

                if (futuras.Count > 0)
                {
                    int menor = futuras.Min(s => s.IdadeInicio);
                    escolhidas = futuras.Where(s => s.IdadeInicio == menor).ToList();
                    futuro = true;
                }
                else
                {
                    var passadas = elegiveis.Where(s => s.IdadeFim < idadeMeses).ToList();
                    if (passadas.Count == 0 || !prm.MostrarForaDaFaixa || idadeMeses >= 120) continue;
                    // Ignora séries que já terminaram há mais tempo que alguma
                    // série posterior da mesma vacina (ex.: dose ao nascer).
                    int fimMaisRecente = passadas.Max(s => s.IdadeFim);
                    escolhidas = passadas.Where(s => s.IdadeFim == fimMaisRecente).ToList();
                    expirado = true;
                }
            }

            var primeira = escolhidas.First();
            bool porGestacao = p.Gestante && escolhidas.Any(s =>
                s.EhGestante && s.Total == 1 && string.IsNullOrWhiteSpace(s.Periodicidade));
            bool porPuerperio = !porGestacao && p.Puerpera && escolhidas.Any(s => s.EhPuerpera && s.Total == 1);

            resultado.Add(new EsquemaPaciente
            {
                Chave = porVacina.Key.Chave,
                Nome = primeira.NomeExibicao,
                Calendario = porVacina.Key.Calendario,
                Series = escolhidas,
                Total = escolhidas.Max(s => s.Total),
                Futuro = futuro,
                Expirado = expirado,
                PorGestacao = porGestacao,
                SemanaGestacionalMinima = porGestacao
                    ? escolhidas.Where(s => s.EhGestante).Max(s => s.SemanaGestacionalMinima)
                    : 0,
                PorPuerperio = porPuerperio
            });
        }

        return resultado;
    }
}
