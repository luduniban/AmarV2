using AmarV.Core.Resultado;

namespace AmarV.Core.Relatorios;

/// <summary>
/// Campos prontos para gravar a análise no banco. Os nomes seguem as colunas
/// da tabela analise_esquema_vacinal do MariaDB (StatusVacinal, VacinasEmAtraso,
/// VacinasPendentes, VacinasDisponiveisSUS, VacinasDisponiveisRedePrivada,
/// SugestaoRecuperacao, VacinasContraindicadas), que até agora ficavam vazias.
/// </summary>
public sealed class ResumoParaBanco
{
    public string StatusVacinal { get; init; } = string.Empty;
    public string VacinasEmAtraso { get; init; } = string.Empty;
    public string VacinasPendentes { get; init; } = string.Empty;
    public string VacinasDisponiveisSUS { get; init; } = string.Empty;
    public string VacinasDisponiveisRedePrivada { get; init; } = string.Empty;
    public string SugestaoRecuperacao { get; init; } = string.Empty;
    public string VacinasContraindicadas { get; init; } = string.Empty;
    public string ResultadoTexto { get; init; } = string.Empty;
    public int QuantidadeAtrasadas { get; init; }
    public int QuantidadePendentes { get; init; }

    public static ResumoParaBanco De(ResultadoAnalise r)
    {
        static string Lista(IEnumerable<string> itens) => string.Join("; ", itens.Distinct());

        var pendentes = r.ParaAplicarHoje.Concat(r.Proximas).ToList();

        return new ResumoParaBanco
        {
            StatusVacinal = r.SituacaoTexto,
            VacinasEmAtraso = Lista(r.Atrasadas.Select(i => $"{i.Vacina} ({i.RotuloProximaDose})")),
            VacinasPendentes = Lista(pendentes.Select(i => $"{i.Vacina} ({i.RotuloProximaDose}" +
                                                         (i.DataMinima.HasValue ? $" a partir de {i.DataMinima:dd/MM/yyyy}" : "") + ")")),
            VacinasDisponiveisSUS = Lista(r.ItensSUS.Where(i => i.Prazo != SituacaoPrazo.ForaDaFaixaEtaria).Select(i => i.Vacina)),
            VacinasDisponiveisRedePrivada = Lista(r.ItensRedePrivada.Select(i => i.Vacina)),
            SugestaoRecuperacao = string.Join(" | ", r.PlanoRecuperacao.Select(a =>
                $"{a.Data:dd/MM/yyyy}: {string.Join(", ", a.Doses.Select(d => d.ToString()))}")),
            VacinasContraindicadas = Lista(r.ComRestricao.Select(i => $"{i.Vacina} [{i.Etiqueta}]")),
            ResultadoTexto = FormatadorTexto.Gerar(r),
            QuantidadeAtrasadas = r.Atrasadas.Count(),
            QuantidadePendentes = pendentes.Count
        };
    }
}
