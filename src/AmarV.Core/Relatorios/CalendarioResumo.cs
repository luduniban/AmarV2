using AmarV.Core.Analise;
using AmarV.Core.Entrada;

namespace AmarV.Core.Relatorios;

/// <summary>Uma linha do calendário para exibição (uma série/esquema de uma vacina).</summary>
public sealed class LinhaCalendario
{
    public string Calendario { get; init; } = string.Empty;
    public string GrupoEtario { get; init; } = string.Empty;
    public string GrupoEspecial { get; init; } = string.Empty;
    public string Vacina { get; init; } = string.Empty;
    public string Esquema { get; init; } = string.Empty;
    public string Faixa { get; init; } = string.Empty;
    public string Via { get; init; } = string.Empty;
    public string Indicacao { get; init; } = string.Empty;
    public string Observacao { get; init; } = string.Empty;
    public int IdadeInicio { get; init; }
}

/// <summary>
/// Monta o calendário a partir das REGRAS cadastradas (RF05). Assim, quando o
/// administrador altera uma regra (RF16/RF17), a consulta do calendário muda junto
/// — antes a tela mostrava um texto fixo.
/// </summary>
public static class CalendarioResumo
{
    public static readonly string[] OrdemGrupos = { "Criança", "Adolescente/Jovem", "Adulto", "Idoso", "Gestante", "Todos" };

    public static List<LinhaCalendario> Montar(IEnumerable<RegraCalendario> regras)
    {
        var linhas = new List<LinhaCalendario>();

        foreach (var s in MontadorEsquemas.MontarSeries(regras))
        {
            var doses = s.Regras.OrderBy(r => r.NumeroDose).ToList();
            bool mesmaIdade = doses.Select(r => r.IdadeMinimaMeses).Distinct().Count() == 1;

            string esquema;
            if (doses.Count == 1 && s.Total == 1)
                esquema = "Dose única";
            else if (!mesmaIdade)
                esquema = $"{s.Total} dose(s): " + string.Join(", ", doses.Select(r => Datas.FormatarIdadeMeses(r.IdadeMinimaMeses)));
            else
                esquema = $"{s.Total} dose(s): " + string.Join(", ", doses.Select(r =>
                    r.NumeroDose == 1 ? "1ª na 1ª visita"
                    : $"{r.NumeroDose}ª após {(string.IsNullOrWhiteSpace(r.IntervaloEntreDoses) ? "intervalo da norma" : r.IntervaloEntreDoses)}"));

            string periodicidade = s.Periodicidade;
            if (!string.IsNullOrWhiteSpace(periodicidade))
                esquema += $" · reforço/repetição: {periodicidade}";
            if (s.SemanaGestacionalMinima > 0)
                esquema += $" · a partir da {s.SemanaGestacionalMinima}ª semana de gestação";

            string inicio = s.IdadeInicio == 0 ? "do nascimento" : $"de {Datas.FormatarIdadeMeses(s.IdadeInicio)}";
            string faixa = s.IdadeFim >= 9999
                ? (s.IdadeInicio == 0 ? "qualquer idade" : $"a partir de {Datas.FormatarIdadeMeses(s.IdadeInicio)}")
                : $"{inicio} até {Datas.FormatarIdadeMeses(s.IdadeFim)} e 29 dias";

            linhas.Add(new LinhaCalendario
            {
                Calendario = s.Calendario,
                GrupoEtario = string.IsNullOrWhiteSpace(s.GrupoEtario) ? "Todos" : s.GrupoEtario,
                GrupoEspecial = CatalogoVacinas.EhGrupoGeral(s.GrupoEspecial) ? string.Empty : s.GrupoEspecial,
                Vacina = s.NomeExibicao,
                Esquema = esquema,
                Faixa = faixa,
                Via = CatalogoVacinas.Via(doses[0].ViaAdministracao),
                Indicacao = doses[0].Indicacao,
                Observacao = string.Join(" ", doses.Select(r => r.Observacao).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct()),
                IdadeInicio = s.IdadeInicio
            });
        }

        return linhas
            .OrderBy(l => l.Calendario == "SUS" ? 0 : 1)
            .ThenBy(l => Array.IndexOf(OrdemGrupos, l.GrupoEtario) is int i && i >= 0 ? i : 99)
            .ThenBy(l => l.GrupoEspecial.Length > 0 ? 1 : 0)
            .ThenBy(l => l.IdadeInicio)
            .ThenBy(l => l.Vacina)
            .ToList();
    }
}
