using AmarV.Core.Entrada;
using AmarV.Core.Resultado;

namespace AmarV.Core.Analise;

/// <summary>
/// Contraindicações e precauções aplicadas sobre o resultado de prazo.
/// Uma restrição NUNCA convive com "liberada": se houver restrição, a
/// vacina não aparece como indicada para hoje (corrige o caso do teste
/// de 09/10/2026, em que a febre amarela aparecia com alerta e liberada).
/// </summary>
public static class RegrasSeguranca
{
    public static (Restricao restricao, string motivo) Avaliar(
        string chave, PerfilPaciente p, int idadeMeses, bool ignorarFebre = false)
    {
        var candidatas = new List<(Restricao r, string m)>();
        bool viva = CatalogoVacinas.EhViva(chave);

        // ---------------- vacinas de agente vivo ----------------
        if (viva && p.TemImunossupressao)
            candidatas.Add((Restricao.Contraindicada,
                "Vacina de agente vivo contraindicada em imunossupressão (imunossupressor, quimioterapia, " +
                "transplante ou corticoide/imunobiológico). Encaminhar ao CRIE para avaliação."));

        if (viva && p.HIV)
            candidatas.Add(chave == CatalogoVacinas.BCG
                ? (Restricao.Contraindicada, "BCG contraindicada em pessoas vivendo com HIV.")
                : (Restricao.Avaliar,
                   "Pessoa vivendo com HIV: vacinas vivas dependem da situação imunológica (CD4). Avaliar no CRIE."));

        if (p.Gestante)
        {
            if (chave == CatalogoVacinas.FebreAmarela)
                candidatas.Add((Restricao.Contraindicada,
                    "Febre amarela contraindicada na gestação, exceto em situação de risco epidemiológico " +
                    "(surto/viagem a área de risco) após avaliação. Programar para após o parto."));
            else if (viva)
                candidatas.Add((Restricao.Contraindicada,
                    "Vacina de agente vivo contraindicada na gestação. Programar para após o parto."));
            else if (chave == CatalogoVacinas.HPV)
                candidatas.Add((Restricao.Adiar,
                    "HPV não é aplicada durante a gestação. Completar o esquema após o parto."));
        }

        if (chave == CatalogoVacinas.FebreAmarela && p.CriancaAmamentadaMenor6Meses)
            candidatas.Add((Restricao.Adiar,
                "Amamenta criança menor de 6 meses: adiar a febre amarela até o bebê completar 6 meses. " +
                "Se a vacinação for imprescindível, suspender o aleitamento por 10 dias (ordenhar e armazenar o leite antes)."));

        if (chave == CatalogoVacinas.Dengue && p.Amamentando)
            candidatas.Add((Restricao.Contraindicada,
                "Vacina dengue (atenuada) contraindicada para quem está amamentando."));

        if (chave == CatalogoVacinas.FebreAmarela && idadeMeses >= 720 && !p.Gestante)
            candidatas.Add((Restricao.Avaliar,
                "A partir de 60 anos, febre amarela somente após avaliação de risco (área com circulação do vírus, " +
                "viagem) e das comorbidades."));

        // ---------------- condição aguda ----------------
        if (p.FebreOuDoencaAguda && !ignorarFebre)
            candidatas.Add((Restricao.Adiar,
                "Doença aguda febril moderada ou grave informada: adiar a vacinação até a melhora do quadro."));

        if (candidatas.Count == 0) return (Restricao.Nenhuma, string.Empty);

        // A restrição mais forte prevalece; os motivos são somados.
        var maisForte = candidatas.Max(c => c.r);
        string motivo = string.Join(" ", candidatas.OrderByDescending(c => c.r).Select(c => c.m).Distinct());
        return (maisForte, motivo);
    }

    /// <summary>Alertas gerais do paciente (não ligados a uma vacina específica).</summary>
    public static List<AlertaClinico> AlertasGerais(PerfilPaciente p, int idadeMeses, IEnumerable<ItemAnalise> itens)
    {
        var alertas = new List<AlertaClinico>();

        if (p.FebreOuDoencaAguda)
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Bloqueio,
                Mensagem = "Febre ou doença aguda informada: nenhuma vacina deve ser aplicada hoje. " +
                           "Reavaliar o paciente após a melhora."
            });

        if (p.AlergiaGrave)
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Atencao,
                Mensagem = "Histórico de alergia grave: conferir os componentes de cada vacina antes de aplicar. " +
                           "Anafilaxia a componente ou a dose anterior contraindica a vacina correspondente."
            });

        if (p.TemImunossupressao || p.HIV)
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Atencao,
                Mensagem = "Condição imunológica especial: vacinas inativadas podem ter resposta reduzida e o CRIE " +
                           "pode indicar esquemas especiais. Vacinas vivas foram marcadas como contraindicadas ou a avaliar."
            });

        if (p.Gestante && p.SemanasGestacao <= 0)
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Atencao,
                Mensagem = "Gestante sem semanas de gestação informadas: dTpa (a partir da 20ª semana) e VSR " +
                           "(a partir da 28ª semana) não podem ser datadas com precisão."
            });

        var hepB = itens.FirstOrDefault(i => i.ChaveVacina == CatalogoVacinas.HepatiteB && !i.EhRedePrivada);
        if (p.ProfissionalSaude && hepB is { Prazo: SituacaoPrazo.EmDia })
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Informacao,
                Vacina = "Hepatite B",
                Mensagem = "Profissional de saúde com esquema completo: solicitar anti-HBs 1 a 2 meses após a última dose."
            });

        if (idadeMeses < 24 &&
            itens.Any(i => i.ChaveVacina == CatalogoVacinas.FebreAmarela && i.Prazo != SituacaoPrazo.EmDia) &&
            itens.Any(i => i.ChaveVacina == CatalogoVacinas.SCR && i.Prazo != SituacaoPrazo.EmDia))
            alertas.Add(new AlertaClinico
            {
                Nivel = NivelAlerta.Informacao,
                Mensagem = "Menor de 2 anos: febre amarela e tríplice viral (SCR) não devem ser aplicadas no mesmo dia " +
                           "na primovacinação. O plano abaixo já separa as duas com 30 dias de intervalo."
            });

        return alertas;
    }
}
