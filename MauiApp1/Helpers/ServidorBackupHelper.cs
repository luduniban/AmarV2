using System.Net.Http.Headers;
using System.Text.Json;

namespace maui_amarv.Helpers;

// Comunicação do backup do banco de dados (.db3) com o servidor,
// usando o mesmo IP e o mesmo token da sincronização.
public static class ServidorBackupHelper
{
    public const string IpPadrao = "192.168.56.1";

    private static string Ip =>
        Preferences.Get("IpServidor", IpPadrao);

    // Valores atuais (para exibir na tela de backup).
    public static string IpAtual => Ip;

    public static bool TemToken =>
        !string.IsNullOrWhiteSpace(
            Preferences.Get("TokenServidor", string.Empty));

    // Salva o IP (e o token, se informado) usados por backup e sincronização.
    public static void SalvarConfiguracao(string ip, string? token)
    {
        Preferences.Set("IpServidor", ip.Trim());

        if (!string.IsNullOrWhiteSpace(token))
            Preferences.Set("TokenServidor", token.Trim());
    }

    private static string UrlBase =>
        $"http://{Ip}/amarv/api";

    private static readonly JsonSerializerOptions OpcoesJson =
        new() { PropertyNameCaseInsensitive = true };

    private static HttpClient CriarCliente(TimeSpan timeout)
    {
        var cliente = new HttpClient { Timeout = timeout };

        string token =
            Preferences.Get("TokenServidor", string.Empty);

        if (!string.IsNullOrWhiteSpace(token))
        {
            cliente.DefaultRequestHeaders.Add(
                "X-Amarv-Token",
                token.Trim());
        }

        return cliente;
    }

    public class RespostaServidor
    {
        public bool sucesso { get; set; }
        public string? mensagem { get; set; }
        public string? arquivo { get; set; }
        public List<BancoNoServidor>? arquivos { get; set; }
    }

    public class BancoNoServidor
    {
        public string nome { get; set; } = "";
        public long tamanho { get; set; }
        public string data { get; set; } = "";

        public DateTime DataHora =>
            DateTime.TryParse(data, out var d) ? d : DateTime.MinValue;
    }

    private static string MensagemDoServidor(
        HttpResponseMessage resposta,
        string corpo)
    {
        try
        {
            var r = JsonSerializer.Deserialize<RespostaServidor>(
                corpo, OpcoesJson);

            if (!string.IsNullOrWhiteSpace(r?.mensagem))
                return r!.mensagem!;
        }
        catch { }

        return $"Erro HTTP {(int)resposta.StatusCode}.";
    }

    // Envia o arquivo do banco para a pasta backups do servidor.
    // Retorna o nome do arquivo criado no servidor.
    public static async Task<string> EnviarBancoAsync(
        string caminhoArquivo)
    {
        using var cliente = CriarCliente(TimeSpan.FromMinutes(5));

        await using FileStream arquivo = File.OpenRead(caminhoArquivo);

        using var conteudo = new StreamContent(arquivo);

        conteudo.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        HttpResponseMessage resposta =
            await cliente.PostAsync(
                $"{UrlBase}/enviar_banco.php",
                conteudo);

        string corpo =
            await resposta.Content.ReadAsStringAsync();

        if (!resposta.IsSuccessStatusCode)
            throw new Exception(MensagemDoServidor(resposta, corpo));

        var resultado =
            JsonSerializer.Deserialize<RespostaServidor>(
                corpo, OpcoesJson);

        if (resultado == null || !resultado.sucesso ||
            string.IsNullOrWhiteSpace(resultado.arquivo))
        {
            throw new Exception(
                resultado?.mensagem ??
                "O servidor não confirmou o recebimento do banco.");
        }

        return resultado.arquivo!;
    }

    // Lista os bancos guardados no servidor (mais recente primeiro).
    public static async Task<List<BancoNoServidor>> ListarBancosAsync()
    {
        using var cliente = CriarCliente(TimeSpan.FromSeconds(30));

        HttpResponseMessage resposta =
            await cliente.GetAsync($"{UrlBase}/listar_bancos.php");

        string corpo =
            await resposta.Content.ReadAsStringAsync();

        if (!resposta.IsSuccessStatusCode)
            throw new Exception(MensagemDoServidor(resposta, corpo));

        var resultado =
            JsonSerializer.Deserialize<RespostaServidor>(
                corpo, OpcoesJson);

        if (resultado == null || !resultado.sucesso)
        {
            throw new Exception(
                resultado?.mensagem ??
                "Resposta inválida do servidor.");
        }

        return resultado.arquivos ?? new List<BancoNoServidor>();
    }

    // Baixa um banco do servidor para o caminho informado.
    public static async Task BaixarBancoAsync(
        string nomeArquivo,
        string caminhoDestino)
    {
        using var cliente = CriarCliente(TimeSpan.FromMinutes(5));

        string url =
            $"{UrlBase}/baixar_banco.php?arquivo=" +
            Uri.EscapeDataString(nomeArquivo);

        using HttpResponseMessage resposta =
            await cliente.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead);

        if (!resposta.IsSuccessStatusCode)
        {
            string corpo =
                await resposta.Content.ReadAsStringAsync();

            throw new Exception(MensagemDoServidor(resposta, corpo));
        }

        await using (var origem =
                         await resposta.Content.ReadAsStreamAsync())
        await using (var destino =
                         new FileStream(
                             caminhoDestino,
                             FileMode.Create,
                             FileAccess.Write))
        {
            await origem.CopyToAsync(destino);
        }

        // Precisa ser um banco SQLite de verdade.
        byte[] cabecalho = new byte[16];

        await using (var teste = File.OpenRead(caminhoDestino))
        {
            int lidos = await teste.ReadAsync(cabecalho, 0, 16);

            string texto =
                System.Text.Encoding.ASCII.GetString(cabecalho, 0, lidos);

            if (lidos < 16 || !texto.StartsWith("SQLite format 3"))
            {
                throw new Exception(
                    "O arquivo recebido não é um banco de dados válido.");
            }
        }
    }
}
