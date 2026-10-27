using System.Text;
using AmarV.Core.Resultado;

namespace AmarV.Core.Relatorios;

/// <summary>
/// Converte o resultado em texto (compartilhar pelo WhatsApp/e-mail, gravar no
/// campo "Resultado" da análise e compatibilidade com a tela antiga).
/// </summary>
public static class FormatadorTexto
{
    public static string Gerar(ResultadoAnalise r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AMAR V — ANÁLISE DO ESQUEMA VACINAL");
        sb.AppendLine(new string('=', 44));
        sb.AppendLine($"Paciente: {r.NomePaciente}");
        if (r.DataNascimento != default)
            sb.AppendLine($"Nascimento: {r.DataNascimento:dd/MM/yyyy} · Idade: {r.IdadeTexto}");
        sb.AppendLine($"Grupo: {r.GrupoEtario}");
        if (r.CondicoesConsideradas.Count > 0)
            sb.AppendLine($"Condições consideradas: {string.Join(", ", r.CondicoesConsideradas)}");
        sb.AppendLine($"Data da análise: {r.DataAnalise:dd/MM/yyyy}");
        sb.AppendLine();
        sb.AppendLine($"SITUAÇÃO VACINAL: {r.SituacaoTexto}" + (r.RequerAvaliacao ? " · REQUER AVALIAÇÃO" : ""));
        sb.AppendLine($"Atrasadas: {r.Atrasadas.Count()} · Aplicar hoje: {r.ParaAplicarHoje.Count()} · " +
                      $"Próximas: {r.Proximas.Count()} · Em dia: {r.EmDia.Count()}");

        foreach (var aviso in r.Avisos)
            sb.AppendLine($"! {aviso}");

        Secao(sb, "ALERTAS", r.Alertas.Select(a => $"[{NomeNivel(a.Nivel)}] {a}"));

        Secao(sb, "VACINAS EM ATRASO (SUS)", r.Atrasadas.Select(Linha));
        Secao(sb, "VACINAS PARA APLICAR HOJE (SUS)", r.ParaAplicarHoje.Select(Linha));
        Secao(sb, "CONTRAINDICAÇÕES E PRECAUÇÕES", r.ComRestricao.Select(i =>
            $"- {i.Vacina} [{i.Etiqueta}]: {i.MotivoRestricao}"));

        if (r.PlanoRecuperacao.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ESQUEMA DE RECUPERAÇÃO / PRÓXIMOS ATENDIMENTOS");
            sb.AppendLine(new string('-', 44));
            foreach (var a in r.PlanoRecuperacao)
            {
                sb.AppendLine($"Atendimento {a.Numero} — {(a.EhHoje ? "hoje, " : "a partir de ")}{a.Data:dd/MM/yyyy}");
                foreach (var d in a.Doses) sb.AppendLine($"   • {d}");
                foreach (var o in a.Observacoes) sb.AppendLine($"   obs.: {o}");
            }
        }

        Secao(sb, "PRÓXIMAS DOSES", r.Proximas.Select(Linha));
        Secao(sb, "ESQUEMAS EM DIA", r.EmDia.Select(i => $"- {i.Vacina}: {i.Motivo}"));
        Secao(sb, "FORA DA FAIXA ETÁRIA (não recuperar)", r.ForaDaFaixa.Select(i => $"- {i.Vacina}: {i.Motivo}"));
        Secao(sb, "REDE PRIVADA (complementares, não entram na situação do SUS)",
            r.ItensRedePrivada.Select(i => $"- {i.Vacina} [{i.Etiqueta}]: {i.Motivo}"));

        sb.AppendLine();
        sb.AppendLine("HISTÓRICO REGISTRADO");
        sb.AppendLine(new string('-', 44));
        if (r.Historico.Count == 0) sb.AppendLine("Nenhuma dose registrada.");
        foreach (var h in r.Historico)
            sb.AppendLine($"- {h.Vacina}: {h.Doses} dose(s)" +
                          (h.UltimaAplicacao.HasValue ? $" — última em {h.UltimaAplicacao:dd/MM/yyyy}" : ""));

        sb.AppendLine();
        sb.AppendLine("A análise é um apoio à decisão e não substitui a avaliação do profissional de saúde. " +
                      "Condições especiais podem exigir avaliação no CRIE.");
        return sb.ToString();
    }

    private static string Linha(ItemAnalise i)
    {
        var s = $"- {i.Vacina} — {i.DescricaoDoses}: {i.Motivo}";
        if (!string.IsNullOrWhiteSpace(i.Via)) s += $" Via: {i.Via}.";
        if (i.Restricao != Restricao.Nenhuma) s += $" [{i.Etiqueta}]";
        return s;
    }

    private static void Secao(StringBuilder sb, string titulo, IEnumerable<string> linhas)
    {
        var lista = linhas.ToList();
        if (lista.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine(titulo);
        sb.AppendLine(new string('-', 44));
        foreach (var l in lista) sb.AppendLine(l);
    }

    internal static string NomeNivel(NivelAlerta n) => n switch
    {
        NivelAlerta.Bloqueio => "BLOQUEIO",
        NivelAlerta.Atencao => "ATENÇÃO",
        _ => "INFO"
    };
}
