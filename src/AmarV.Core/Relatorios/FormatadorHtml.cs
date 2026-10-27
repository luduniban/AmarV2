using System.Net;
using System.Text;
using AmarV.Core.Resultado;

namespace AmarV.Core.Relatorios;

/// <summary>Relatório individual em HTML (RF07) — pronto para imprimir ou salvar em PDF pelo navegador.</summary>
public static class FormatadorHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public const string Estilo = """
        :root{--azul:#1f3c88;--verde:#2b7a78;--vermelho:#b42318;--laranja:#b54708;--cinza:#475467;--fundo:#f6f8fb;--borda:#d0d5dd}
        *{box-sizing:border-box}body{font-family:Segoe UI,Roboto,Arial,sans-serif;color:#101828;margin:0;background:var(--fundo)}
        .pagina{max-width:900px;margin:0 auto;padding:24px;background:#fff}
        h1{color:var(--azul);font-size:22px;margin:0 0 4px}h2{font-size:16px;color:var(--azul);border-bottom:2px solid var(--borda);padding-bottom:4px;margin-top:28px}
        .sub{color:var(--cinza);font-size:13px}.cab{display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap}
        .situacao{padding:10px 14px;border-radius:8px;font-weight:700;color:#fff;display:inline-block}
        .s-regular{background:var(--verde)}.s-aplicar{background:var(--laranja)}.s-atraso{background:var(--vermelho)}.s-na{background:var(--cinza)}
        .contagem{display:flex;gap:10px;flex-wrap:wrap;margin-top:10px}.contagem div{border:1px solid var(--borda);border-radius:8px;padding:6px 10px;font-size:13px}
        table{width:100%;border-collapse:collapse;font-size:13px}th,td{border-bottom:1px solid var(--borda);padding:6px;text-align:left;vertical-align:top}th{background:#eef2f6}
        .tag{display:inline-block;padding:2px 8px;border-radius:10px;font-size:11px;font-weight:700;color:#fff;white-space:nowrap}
        .t-atrasada{background:var(--vermelho)}.t-hoje{background:var(--laranja)}.t-futura{background:#667085}.t-emdia{background:var(--verde)}
        .t-restr{background:#6941c6}.t-fora{background:#98a2b3}
        .alerta{border-left:4px solid var(--laranja);background:#fffaeb;padding:8px 10px;margin:6px 0;font-size:13px}
        .alerta.bloq{border-color:var(--vermelho);background:#fef3f2}.alerta.info{border-color:var(--azul);background:#eff4ff}
        .atend{border:1px solid var(--borda);border-radius:8px;padding:8px 12px;margin:8px 0}.atend b{color:var(--azul)}
        .atend ul{margin:6px 0 0 18px;padding:0}.obs{color:var(--cinza);font-size:12px}
        .rodape{margin-top:28px;font-size:12px;color:var(--cinza);border-top:1px solid var(--borda);padding-top:8px}
        @media print{body{background:#fff}.pagina{padding:0}}
        """;

    public static string Gerar(ResultadoAnalise r)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>Amar V — Situação vacinal — {E(r.NomePaciente)}</title><style>{Estilo}</style></head><body><div class=\"pagina\">");

        string classe = r.Situacao switch
        {
            SituacaoGeral.Regular => "s-regular",
            SituacaoGeral.ComDosesParaAplicar => "s-aplicar",
            SituacaoGeral.EmAtraso => "s-atraso",
            _ => "s-na"
        };

        sb.Append("<div class=\"cab\"><div>");
        sb.Append("<h1>Relatório da situação vacinal</h1>");
        sb.Append($"<div><b>{E(r.NomePaciente)}</b></div>");
        sb.Append($"<div class=\"sub\">Nascimento: {r.DataNascimento:dd/MM/yyyy} · Idade: {E(r.IdadeTexto)} · Grupo: {E(r.GrupoEtario)}</div>");
        if (r.CondicoesConsideradas.Count > 0)
            sb.Append($"<div class=\"sub\">Condições consideradas: {E(string.Join(", ", r.CondicoesConsideradas))}</div>");
        sb.Append($"<div class=\"sub\">Análise em {r.DataAnalise:dd/MM/yyyy}</div>");
        sb.Append("</div><div>");
        sb.Append($"<span class=\"situacao {classe}\">{E(r.SituacaoTexto)}</span>");
        if (r.RequerAvaliacao) sb.Append("<div class=\"sub\" style=\"margin-top:6px\">⚠ requer avaliação do profissional</div>");
        sb.Append("</div></div>");

        sb.Append("<div class=\"contagem\">");
        sb.Append($"<div>Atrasadas: <b>{r.Atrasadas.Count()}</b></div>");
        sb.Append($"<div>Aplicar hoje: <b>{r.ParaAplicarHoje.Count()}</b></div>");
        sb.Append($"<div>Próximas: <b>{r.Proximas.Count()}</b></div>");
        sb.Append($"<div>Em dia: <b>{r.EmDia.Count()}</b></div>");
        sb.Append($"<div>Com restrição: <b>{r.ComRestricao.Count()}</b></div>");
        sb.Append("</div>");

        foreach (var a in r.Avisos) sb.Append($"<div class=\"alerta\">{E(a)}</div>");

        if (r.Alertas.Count > 0)
        {
            sb.Append("<h2>Alertas</h2>");
            foreach (var a in r.Alertas)
            {
                string c = a.Nivel switch { NivelAlerta.Bloqueio => "alerta bloq", NivelAlerta.Informacao => "alerta info", _ => "alerta" };
                sb.Append($"<div class=\"{c}\">{E(a.ToString())}</div>");
            }
        }

        Tabela(sb, "Vacinas em atraso e para aplicar hoje (SUS)", r.Atrasadas.Concat(r.ParaAplicarHoje).ToList());
        Tabela(sb, "Contraindicações e precauções", r.ComRestricao.ToList(), mostrarRestricao: true);

        if (r.PlanoRecuperacao.Count > 0)
        {
            sb.Append("<h2>Esquema de recuperação — próximos atendimentos</h2>");
            foreach (var a in r.PlanoRecuperacao)
            {
                sb.Append($"<div class=\"atend\"><b>Atendimento {a.Numero}</b> — {(a.EhHoje ? "hoje, " : "a partir de ")}{a.Data:dd/MM/yyyy}<ul>");
                foreach (var d in a.Doses) sb.Append($"<li>{E(d.ToString())}</li>");
                sb.Append("</ul>");
                foreach (var o in a.Observacoes) sb.Append($"<div class=\"obs\">{E(o)}</div>");
                sb.Append("</div>");
            }
        }

        Tabela(sb, "Próximas doses", r.Proximas.ToList());
        Tabela(sb, "Esquemas em dia", r.EmDia.ToList());
        Tabela(sb, "Fora da faixa etária (não recuperar)", r.ForaDaFaixa.ToList());
        Tabela(sb, "Rede privada (complementares — não entram na situação do SUS)", r.ItensRedePrivada);

        sb.Append("<h2>Histórico registrado</h2>");
        if (r.Historico.Count == 0) sb.Append("<p class=\"sub\">Nenhuma dose registrada.</p>");
        else
        {
            sb.Append("<table><tr><th>Vacina</th><th>Doses</th><th>Primeira</th><th>Última</th></tr>");
            foreach (var h in r.Historico)
                sb.Append($"<tr><td>{E(h.Vacina)}</td><td>{h.Doses}</td><td>{h.PrimeiraAplicacao:dd/MM/yyyy}</td><td>{h.UltimaAplicacao:dd/MM/yyyy}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<div class=\"rodape\">Amar V — Aplicativo de apoio à regularização vacinal. ");
        sb.Append("A análise é um apoio à decisão e não substitui a avaliação do profissional de saúde; ");
        sb.Append("condições especiais podem exigir avaliação no CRIE. ");
        string fonte = r.ItensSUS.Select(i => i.Fonte).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)) ?? "";
        if (fonte.Length > 0) sb.Append($"Referência: {E(fonte)}. ");
        sb.Append($"{E(r.VersaoMotor)}.</div>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Tabela(StringBuilder sb, string titulo, List<ItemAnalise> itens, bool mostrarRestricao = false)
    {
        if (itens.Count == 0) return;
        sb.Append($"<h2>{E(titulo)}</h2><table><tr><th>Vacina</th><th>Situação</th><th>Doses</th><th>Detalhe</th></tr>");
        foreach (var i in itens)
        {
            string tag = i.Restricao != Restricao.Nenhuma ? "t-restr" : i.Prazo switch
            {
                SituacaoPrazo.Atrasada => "t-atrasada",
                SituacaoPrazo.AplicarHoje => "t-hoje",
                SituacaoPrazo.EmDia => "t-emdia",
                SituacaoPrazo.ForaDaFaixaEtaria => "t-fora",
                _ => "t-futura"
            };
            string detalhe = mostrarRestricao ? i.MotivoRestricao : i.Motivo;
            if (!mostrarRestricao && !string.IsNullOrWhiteSpace(i.Via)) detalhe += $" Via: {i.Via}.";
            sb.Append($"<tr><td><b>{E(i.Vacina)}</b></td><td><span class=\"tag {tag}\">{E(i.Etiqueta)}</span></td>");
            sb.Append($"<td>{E(i.DescricaoDoses)}</td><td>{E(detalhe)}</td></tr>");
        }
        sb.Append("</table>");
    }
}
