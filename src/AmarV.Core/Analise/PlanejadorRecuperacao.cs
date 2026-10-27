using AmarV.Core.Entrada;
using AmarV.Core.Resultado;

namespace AmarV.Core.Analise;

/// <summary>
/// Monta o esquema de recuperação (RF13) em ATENDIMENTOS com data:
/// "Atendimento 1 — hoje: Penta 1ª, VIP 1ª...", "Atendimento 2 — a partir de 23/11: ...".
///
/// Regras usadas na distribuição:
///  • intervalo mínimo entre doses da mesma vacina (das regras cadastradas);
///  • idade mínima e máxima de cada dose;
///  • vacinas vivas injetáveis: no mesmo dia ou com 4 semanas de intervalo;
///  • menores de 2 anos: febre amarela e SCR não no mesmo dia (30 dias);
///  • limite operacional de aplicações SC/IM por atendimento (parâmetro da equipe);
///  • vacinas contraindicadas ficam fora do plano.
/// </summary>
public static class PlanejadorRecuperacao
{
    private sealed class Fila
    {
        public required ItemAnalise Item { get; init; }
        public int Indice { get; set; }
        public DateTime Liberacao { get; set; }
        public DateTime? PrimeiraDose { get; set; }
        public DoseProjetada Atual => Item.DosesRestantes[Indice];
    }

    public static List<Atendimento> Montar(IEnumerable<ItemAnalise> itensSUS, PerfilPaciente p, ParametrosAnalise prm)
    {
        DateTime hoje = prm.DataReferencia.Date;
        DateTime inicio = p.FebreOuDoencaAguda ? hoje.AddDays(Math.Max(1, prm.RetornoDias)) : hoje;
        DateTime fim = hoje.AddMonths(Math.Max(1, prm.HorizonteMeses));
        DateTime nasc = p.DataNascimento.Date;

        var filas = itensSUS
            .Where(i => !i.ForaDoPlano && i.Restricao != Restricao.Contraindicada &&
                        i.Prazo is SituacaoPrazo.Atrasada or SituacaoPrazo.AplicarHoje
                                 or SituacaoPrazo.AguardandoIntervalo or SituacaoPrazo.Futura &&
                        i.DosesRestantes.Count > 0)
            .Select(i => new Fila
            {
                Item = i, Indice = 0,
                Liberacao = Datas.Max(inicio, i.DosesRestantes[0].DataPrevista),
                PrimeiraDose = i.DosesRegistradas > 0 ? i.PrimeiraAplicacao : null
            })
            .ToList();

        var atendimentos = new List<Atendimento>();
        var vivasAplicadas = new List<DateTime>();
        if (filas.Count == 0) return atendimentos;

        DateTime dia = filas.Min(f => f.Liberacao);
        int seguranca = 0;

        while (filas.Count > 0 && dia <= fim && seguranca++ < 200)
        {
            var candidatas = filas
                .Where(f => f.Liberacao <= dia)
                .OrderBy(f => f.Item.Prazo == SituacaoPrazo.Atrasada ? 0 : f.Item.Prazo == SituacaoPrazo.AplicarHoje ? 1 : 2)
                .ThenBy(f => f.Atual.DataLimite ?? DateTime.MaxValue)
                .ThenBy(f => f.Item.Vacina)
                .ToList();

            var atendimento = new Atendimento { Data = dia, EhHoje = dia == hoje };
            int sc = 0, im = 0;
            int idadeNoDia = Datas.IdadeEmMeses(nasc, dia);
            int limiteIM = idadeNoDia <= 24 ? prm.LimiteIntramuscularesAte24Meses : prm.LimiteIntramuscularesAcima24Meses;

            foreach (var f in candidatas)
            {
                var dose = f.Atual;
                string chave = f.Item.ChaveVacina;

                // Dose que já não cabe na idade máxima
                if (dose.DataLimite.HasValue && dia > dose.DataLimite.Value)
                {
                    atendimento.Observacoes.Add(
                        $"{f.Item.Vacina}: a {dose.NumeroDose}ª dose ultrapassa a idade máxima ({dose.DataLimite:dd/MM/yyyy}) e saiu do plano.");
                    filas.Remove(f);
                    continue;
                }

                // Vacinas vivas injetáveis: mesmo dia ou 4 semanas
                if (CatalogoVacinas.EhVivaInjetavel(chave))
                {
                    var conflito = vivasAplicadas
                        .Where(d => d != dia && (dia - d).TotalDays < prm.IntervaloVivasInjetaveisDias)
                        .DefaultIfEmpty(DateTime.MinValue).Max();
                    if (conflito != DateTime.MinValue)
                    {
                        f.Liberacao = conflito.AddDays(prm.IntervaloVivasInjetaveisDias);
                        continue;
                    }

                    if (idadeNoDia < 24 && dose.NumeroDose == 1 &&
                        ((chave == CatalogoVacinas.FebreAmarela && atendimento.Doses.Any(d => d.ChaveVacina == CatalogoVacinas.SCR)) ||
                         (chave == CatalogoVacinas.SCR && atendimento.Doses.Any(d => d.ChaveVacina == CatalogoVacinas.FebreAmarela))))
                    {
                        f.Liberacao = dia.AddDays(30);
                        continue;
                    }
                }

                // Limite operacional por atendimento
                if (prm.AplicarLimitesPorAtendimento)
                {
                    if (dose.Via == "SC" && sc >= prm.LimiteSubcutaneas ||
                        dose.Via == "IM" && im >= limiteIM)
                    {
                        f.Liberacao = dia.AddDays(Math.Max(1, prm.RetornoDias));
                        continue;
                    }
                }

                if (dose.Via == "SC") sc++;
                if (dose.Via == "IM") im++;
                if (CatalogoVacinas.EhVivaInjetavel(chave)) vivasAplicadas.Add(dia);

                atendimento.Doses.Add(new DosePlanejada
                {
                    Vacina = f.Item.Vacina,
                    ChaveVacina = chave,
                    NumeroDose = dose.NumeroDose,
                    EhReforco = f.Item.ProximaEhReforco && f.Indice == 0,
                    Rotulo = f.Indice == 0 ? f.Item.RotuloProximaDose : $"{dose.NumeroDose}ª dose",
                    RegraId = dose.RegraId,
                    Via = dose.Via,
                    Atrasada = f.Item.Prazo == SituacaoPrazo.Atrasada && f.Indice == 0,
                    RequerAvaliacao = f.Item.Restricao is Restricao.Avaliar or Restricao.Adiar
                });

                // Próxima dose da mesma vacina
                f.PrimeiraDose ??= dia;
                f.Indice++;
                if (f.Indice >= f.Item.DosesRestantes.Count)
                {
                    filas.Remove(f);
                    continue;
                }

                var proxima = f.Atual;
                var intervalo = Duracao.Interpretar(proxima.Intervalo);
                f.Liberacao = Datas.Max(proxima.DataIdadeMinima, intervalo.Vazia ? dia.AddDays(1) : intervalo.SomarA(dia));
                if (proxima.DiasMinimosDesdePrimeira > 0 && f.PrimeiraDose.HasValue)
                    f.Liberacao = Datas.Max(f.Liberacao, f.PrimeiraDose.Value.AddDays(proxima.DiasMinimosDesdePrimeira));
            }

            if (atendimento.Doses.Count > 0 || atendimento.Observacoes.Count > 0)
                atendimentos.Add(atendimento);

            if (filas.Count == 0) break;
            DateTime proximoDia = filas.Min(f => f.Liberacao);
            if (proximoDia <= dia) proximoDia = dia.AddDays(1);

            // Agrupa doses próximas no mesmo retorno (menos idas à unidade):
            // adia a mais cedo em até N dias para juntar com as seguintes, sem passar da idade máxima.
            if (prm.JanelaAgrupamentoDias > 0)
            {
                DateTime limiteJanela = proximoDia.AddDays(prm.JanelaAgrupamentoDias);
                var agrupaveis = filas.Where(f => f.Liberacao >= proximoDia && f.Liberacao <= limiteJanela).ToList();
                DateTime alvo = agrupaveis.Max(f => f.Liberacao);
                bool cabe = agrupaveis.All(f => !f.Atual.DataLimite.HasValue || alvo <= f.Atual.DataLimite.Value);
                if (cabe) proximoDia = alvo;
            }
            dia = proximoDia;
        }

        // Junta observações soltas (sem doses) ao atendimento seguinte e numera
        var finais = new List<Atendimento>();
        var pendentes = new List<string>();
        foreach (var a in atendimentos)
        {
            if (a.Doses.Count == 0) { pendentes.AddRange(a.Observacoes); continue; }
            a.Observacoes.InsertRange(0, pendentes);
            pendentes.Clear();
            finais.Add(a);
        }
        if (pendentes.Count > 0 && finais.Count > 0) finais[^1].Observacoes.AddRange(pendentes);

        for (int i = 0; i < finais.Count; i++) finais[i].Numero = i + 1;

        if (p.FebreOuDoencaAguda && finais.Count > 0)
            finais[0].Observacoes.Insert(0, "Plano iniciado após a melhora da febre/doença aguda (reavaliar o paciente).");

        return finais;
    }
}
