using System.Text.Json;

namespace AmarV.Core.Entrada;

/// <summary>Lê regras exportadas em JSON (mesmos nomes de coluna da tabela regra_vacinal).</summary>
public static class CarregadorRegras
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNameCaseInsensitive = true };

    public static List<RegraCalendario> DeJson(string json)
    {
        var regras = JsonSerializer.Deserialize<List<RegraCalendario>>(json, Opcoes) ?? new();
        foreach (var r in regras)
        {
            r.NomeVacina ??= string.Empty;
            r.GrupoEtario ??= string.Empty;
            r.GrupoEspecial ??= string.Empty;
            r.IntervaloEntreDoses ??= string.Empty;
            r.Periodicidade ??= string.Empty;
            r.ViaAdministracao ??= string.Empty;
            r.TipoCalendario ??= string.Empty;
            r.FonteCalendario ??= string.Empty;
            r.Indicacao ??= string.Empty;
            r.Contraindicacao ??= string.Empty;
            r.Observacao ??= string.Empty;
        }
        return regras;
    }

    public static List<RegraCalendario> DeArquivo(string caminho) => DeJson(File.ReadAllText(caminho));
}
