using System.Net;
using System.Text;
using AmarV.Core.Resultado;

namespace AmarV.Core.Relatorios;

/// <summary>Dados de contato do paciente usados nos relatórios administrativos.</summary>
public sealed class PacienteRelatorio
{
    public required ResultadoAnalise Analise { get; init; }
    public string NomeMae { get; init; } = string.Empty;
    public string Telefone { get; init; } = string.Empty;
    public string Endereco { get; init; } = string.Empty;
}

public enum TipoRelatorio { PacientesEmAtraso, PacientesRegulares, Geral }

/// <summary>
/// Relatórios administrativos RF20 (pacientes com vacinas atrasadas), RF21
/// (pacientes em situação regular) e geral. Usam a MESMA análise da tela do
/// paciente — antes, os relatórios tinham um cálculo próprio (CalcularPendencias)
/// que dava resultados diferentes da análise.
/// </summary>
public static class RelatorioConsolidado
{
    public static List<PacienteRelatorio> Filtrar(IEnumerable<PacienteRelatorio> pacientes, TipoRelatorio tipo) => tipo switch
    {
        TipoRelatorio.PacientesEmAtraso => pacientes.Where(p => p.Analise.Situacao == SituacaoGeral.EmAtraso)
                                                    .OrderByDescending(p => p.Analise.Atrasadas.Count())
                                                    .ThenBy(p => p.Analise.NomePaciente).ToList(),
        TipoRelatorio.PacientesRegulares => pacientes.Where(p => p.Analise.Situacao == SituacaoGeral.Regular)
                                                     .OrderBy(p => p.Analise.NomePaciente).ToList(),
        _ => pacientes.OrderBy(p => p.Analise.NomePaciente).ToList()
    };

    public static string Titulo(TipoRelatorio tipo) => tipo switch
    {
        TipoRelatorio.PacientesEmAtraso => "Relatório de pacientes com vacinas atrasadas",
        TipoRelatorio.PacientesRegulares => "Relatório de pacientes em situação regular",
        _ => "Relatório geral da situação vacinal"
    };

    public static string Texto(IEnumerable<PacienteRelatorio> todos, TipoRelatorio tipo, DateTime data)
    {
        var lista = todos.ToList();
        var filtrados = Filtrar(lista, tipo);
        var sb = new StringBuilder();
        sb.AppendLine(Titulo(tipo).ToUpperInvariant());
        sb.AppendLine($"Gerado em {data:dd/MM/yyyy HH:mm}");
        sb.AppendLine(Resumo(lista));
        sb.AppendLine();

        if (tipo == TipoRelatorio.Geral)
        {
            sb.AppendLine("VACINAS MAIS ATRASADAS");
            foreach (var (vacina, qtd) in RankingAtrasos(lista))
                sb.AppendLine($"- {vacina}: {qtd} paciente(s)");
            sb.AppendLine();
        }

        if (filtrados.Count == 0) sb.AppendLine("Nenhum paciente encontrado.");
        foreach (var p in filtrados)
        {
            var a = p.Analise;
            sb.AppendLine($"Paciente: {a.NomePaciente} · {a.IdadeTexto} · {a.SituacaoTexto}");
            if (!string.IsNullOrWhiteSpace(p.NomeMae)) sb.AppendLine($"Nome da mãe: {p.NomeMae}");
            if (!string.IsNullOrWhiteSpace(p.Telefone)) sb.AppendLine($"Telefone: {p.Telefone}");
            if (!string.IsNullOrWhiteSpace(p.Endereco)) sb.AppendLine($"Endereço: {p.Endereco}");
            if (a.Atrasadas.Any())
                sb.AppendLine($"Atrasadas: {string.Join(", ", a.Atrasadas.Select(i => $"{i.Vacina} ({i.RotuloProximaDose})"))}");
            if (a.ParaAplicarHoje.Any())
                sb.AppendLine($"Aplicar: {string.Join(", ", a.ParaAplicarHoje.Select(i => i.Vacina))}");
            if (tipo == TipoRelatorio.PacientesRegulares && a.Proximas.Any())
                sb.AppendLine($"Próxima vacina: {a.Proximas.First().Vacina} em {a.Proximas.First().DataMinima:dd/MM/yyyy}");
            if (a.RequerAvaliacao) sb.AppendLine("Requer avaliação do profissional (restrições/alertas).");
            sb.AppendLine(new string('-', 30));
        }
        return sb.ToString();
    }

    public static string Csv(IEnumerable<PacienteRelatorio> todos, TipoRelatorio tipo)
    {
        static string C(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("Paciente;Idade;Situacao;Atrasadas;AplicarHoje;ProximaVacina;DataProxima;RequerAvaliacao;NomeMae;Telefone;Endereco");
        foreach (var p in Filtrar(todos, tipo))
        {
            var a = p.Analise;
            var prox = a.Proximas.FirstOrDefault();
            sb.AppendLine(string.Join(";",
                C(a.NomePaciente), C(a.IdadeTexto), C(a.SituacaoTexto),
                C(string.Join(", ", a.Atrasadas.Select(i => i.Vacina))),
                C(string.Join(", ", a.ParaAplicarHoje.Select(i => i.Vacina))),
                C(prox?.Vacina), C(prox?.DataMinima?.ToString("dd/MM/yyyy")),
                a.RequerAvaliacao ? "Sim" : "Não",
                C(p.NomeMae), C(p.Telefone), C(p.Endereco)));
        }
        return sb.ToString();
    }

    public static string Html(IEnumerable<PacienteRelatorio> todos, TipoRelatorio tipo, DateTime data)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var lista = todos.ToList();
        var filtrados = Filtrar(lista, tipo);
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{E(Titulo(tipo))}</title>");
        sb.Append($"<style>{FormatadorHtml.Estilo}</style></head><body><div class=\"pagina\">");
        sb.Append($"<h1>{E(Titulo(tipo))}</h1><div class=\"sub\">Gerado em {data:dd/MM/yyyy HH:mm} · {E(Resumo(lista))}</div>");

        if (tipo == TipoRelatorio.Geral)
        {
            sb.Append("<h2>Vacinas mais atrasadas</h2><table><tr><th>Vacina</th><th>Pacientes</th></tr>");
            foreach (var (vacina, qtd) in RankingAtrasos(lista))
                sb.Append($"<tr><td>{E(vacina)}</td><td>{qtd}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<h2>Pacientes</h2>");
        if (filtrados.Count == 0) { sb.Append("<p class=\"sub\">Nenhum paciente encontrado.</p>"); }
        else
        {
            sb.Append("<table><tr><th>Paciente</th><th>Situação</th><th>Vacinas</th><th>Contato</th></tr>");
            foreach (var p in filtrados)
            {
                var a = p.Analise;
                string tag = a.Situacao switch
                {
                    SituacaoGeral.EmAtraso => "t-atrasada", SituacaoGeral.ComDosesParaAplicar => "t-hoje",
                    SituacaoGeral.Regular => "t-emdia", _ => "t-fora"
                };
                string vacinas = a.Atrasadas.Any()
                    ? "Atrasadas: " + string.Join(", ", a.Atrasadas.Select(i => i.Vacina))
                    : a.ParaAplicarHoje.Any() ? "Aplicar: " + string.Join(", ", a.ParaAplicarHoje.Select(i => i.Vacina))
                    : a.Proximas.Any() ? $"Próxima: {a.Proximas.First().Vacina} em {a.Proximas.First().DataMinima:dd/MM/yyyy}" : "—";
                sb.Append($"<tr><td><b>{E(a.NomePaciente)}</b><div class=\"sub\">{E(a.IdadeTexto)}</div></td>");
                sb.Append($"<td><span class=\"tag {tag}\">{E(a.SituacaoTexto)}</span>{(a.RequerAvaliacao ? "<div class=\"sub\">⚠ avaliar</div>" : "")}</td>");
                sb.Append($"<td>{E(vacinas)}</td><td>{E(p.Telefone)}<div class=\"sub\">{E(p.Endereco)}</div>");
                if (!string.IsNullOrWhiteSpace(p.NomeMae)) sb.Append($"<div class=\"sub\">Mãe: {E(p.NomeMae)}</div>");
                sb.Append("</td></tr>");
            }
            sb.Append("</table>");
        }
        sb.Append("<div class=\"rodape\">Amar V — relatório administrativo gerado a partir da análise vacinal de cada paciente.</div></div></body></html>");
        return sb.ToString();
    }

    public static string Resumo(List<PacienteRelatorio> lista)
    {
        int total = lista.Count;
        int atraso = lista.Count(p => p.Analise.Situacao == SituacaoGeral.EmAtraso);
        int aplicar = lista.Count(p => p.Analise.Situacao == SituacaoGeral.ComDosesParaAplicar);
        int regular = lista.Count(p => p.Analise.Situacao == SituacaoGeral.Regular);
        int semAnalise = lista.Count(p => p.Analise.Situacao == SituacaoGeral.NaoAnalisado);
        string pct(int n) => total == 0 ? "0%" : $"{100.0 * n / total:0}%";
        return $"Total: {total} · Em atraso: {atraso} ({pct(atraso)}) · Com doses para aplicar: {aplicar} ({pct(aplicar)}) · " +
               $"Regulares: {regular} ({pct(regular)})" + (semAnalise > 0 ? $" · Sem análise (sem nascimento): {semAnalise}" : "");
    }

    public static List<(string vacina, int pacientes)> RankingAtrasos(IEnumerable<PacienteRelatorio> lista) =>
        lista.SelectMany(p => p.Analise.Atrasadas.Select(i => i.Vacina).Distinct())
             .GroupBy(v => v).Select(g => (g.Key, g.Count()))
             .OrderByDescending(x => x.Item2).ThenBy(x => x.Key).ToList();
}
