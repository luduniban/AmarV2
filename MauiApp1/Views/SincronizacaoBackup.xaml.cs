using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using maui_amarv.Models;

namespace maui_amarv.Views;

public partial class SincronizacaoBackup : ContentPage
{
    private UsuarioDoSistema usuarioLogado;

    private IDispatcherTimer? _relogioTimer;

    // =====================================================
    // CONFIGURAÇÃO DO SERVIDOR
    // =====================================================

    private const string IpPadrao = "192.168.88.254";

    private string IpServidor
    {
        get
        {
            return Preferences.Get(
                "IpServidor",
                IpPadrao);
        }
    }

    private string UrlBackup =>
        $"http://{IpServidor}/amarv/api/backup.php";

    private string UrlEnviarBanco =>
        $"http://{IpServidor}/amarv/api/enviar_banco.php";

    private string UrlRestaurar =>
        $"http://{IpServidor}/amarv/api/restaurar.php";

    private string UrlSincronizacao =>
        $"http://{IpServidor}/amarv/api/sincronizar.php";

    private string UrlBaixarDados =>
        $"http://{IpServidor}/amarv/api/baixar_dados.php";


    // =====================================================
    // OPÇÕES DO JSON
    // =====================================================

    // Registros que não puderam ser gravados na recuperação
    // (paciente ou usuário com cadastro incompleto).
    private readonly List<string> _avisosRecuperacao = new();

    private JsonSerializerOptions CriarOpcoesJson()
    {
        var opcoes = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        opcoes.Converters.Add(new MySqlDateTimeConverter());
        opcoes.Converters.Add(new MySqlBooleanConverter());

        return opcoes;
    }


    // =====================================================
    // SALVAR IP DO SERVIDOR
    // =====================================================

    private async void Button_SalvarIp_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            string ip = txt_IpServidor.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(ip))
            {
                await DisplayAlert(
                    "IP inválido",
                    "Digite o IP do servidor.",
                    "OK");

                return;
            }

            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                await DisplayAlert(
                    "IP inválido",
                    "Digite um endereço IP válido.\n\n" +
                    "Exemplo:\n" +
                    "192.168.88.254",
                    "OK");

                return;
            }

            Preferences.Set(
                "IpServidor",
                ip);

            string token = txt_TokenServidor.Text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(token))
            {
                Preferences.Set(
                    "TokenServidor",
                    token);
            }

            lbl_status.Text =
                "IP do servidor salvo:\n" +
                ip;

            await DisplayAlert(
                "Servidor",
                "IP do servidor salvo com sucesso.\n\n" +
                "IP:\n" +
                ip,
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Erro",
                "Não foi possível salvar o IP.\n\n" +
                ex.Message,
                "OK");
        }
    }


    // =====================================================
    // CONSTRUTOR
    // =====================================================


    public SincronizacaoBackup(UsuarioDoSistema usuario)
    {
        InitializeComponent();

        usuarioLogado = usuario;

        // Somente o Administrador visualiza a área de backup.
        bool administrador = UsuarioAdministrador();

        lbl_titulo_backup.IsVisible = administrador;
        btn_backup_servidor.IsVisible = administrador;
        btn_enviar_banco.IsVisible = administrador;
        btn_restaurar_backup.IsVisible = administrador;

        // Mantém o relógio e as demais funções da tela.
        IniciarRelogio();
    }


    // =====================================================
    // RELÓGIO
    // =====================================================

    private void IniciarRelogio()
    {
        AtualizarRelogio();

        _relogioTimer =
            Dispatcher.CreateTimer();

        _relogioTimer.Interval =
            TimeSpan.FromSeconds(1);

        _relogioTimer.Tick += (s, e) =>
        {
            AtualizarRelogio();
        };

        _relogioTimer.Start();
    }


    private void AtualizarRelogio()
    {
        lbl_relogio.Text =
            DateTime.Now.ToString(
                "dd/MM/yyyy HH:mm:ss");
    }


    // =====================================================
    // CLIENTE HTTP
    // =====================================================

    private HttpClient CriarCliente()
    {
        var cliente = new HttpClient();

        cliente.Timeout =
            TimeSpan.FromSeconds(30);

        // Token exigido pelo servidor (config.local.php: API_TOKEN).
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


    // =====================================================
    // BACKUP
    // =====================================================

    private async void Button_Backup_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            if (!UsuarioAdministrador())
            {
                await ExibirAcessoNegado();
                return;
            }

            lbl_status.Text =
                "Preparando backup...";

            var pessoas =
    await App.Database.Listar<Pessoa>();

            var pacientes =
                await App.Database.ListarPacientes();

            var historico =
                await App.Database.ListarHistoricoVacinal();

            var backup = new
            {
                aplicativo = "Amar V",

                usuario = new
                {
                    nome = usuarioLogado.Nome,
                    perfil = usuarioLogado.Perfil
                },

                dataBackup = DateTime.Now,

                pessoa = pessoas,

                pacientes = pacientes,

                historico = historico
            };

            string json =
                JsonSerializer.Serialize(
                    backup,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            lbl_status.Text =
                "Enviando backup para o servidor...";

            using HttpClient cliente =
                CriarCliente();

            using StringContent conteudo =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            HttpResponseMessage resposta =
                await cliente.PostAsync(
                    UrlBackup,
                    conteudo);

            string respostaTexto =
                await resposta.Content
                    .ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Erro HTTP: " +
                    (int)resposta.StatusCode +
                    "\n\n" +
                    respostaTexto);
            }

            var resultado =
                JsonSerializer.Deserialize<RespostaBackup>(
                    respostaTexto,
                    CriarOpcoesJson());

            if (resultado == null ||
                !resultado.sucesso)
            {
                throw new Exception(
                    resultado?.mensagem ??
                    "Servidor não confirmou o backup.");
            }

            Backup registroBackup =
                new Backup
                {
                    DataBackup =
                        DateTime.Now,

                    Status =
                        "Backup enviado ao servidor",

                    LocalArquivo =
                        resultado.arquivo
                };

            registroBackup.RealizarBackup();

            await App.Database.SalvarBackup(registroBackup);

            lbl_status.Text =
                "Backup realizado com sucesso!\n" +
                "Arquivo: " +
                resultado.arquivo;

            await DisplayAlert(
                "Backup",
                "Backup enviado para o servidor com sucesso.\n\n" +
                "Arquivo: " +
                resultado.arquivo,
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text =
                "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                "O aplicativo não conseguiu se conectar ao servidor.\n\n" +
                "Endereço:\n" +
                UrlBackup +
                "\n\nDetalhes:\n" +
                ex.Message,
                "OK");
        }
        catch (TaskCanceledException)
        {
            lbl_status.Text =
                "Tempo de conexão esgotado.";

            await DisplayAlert(
                "Tempo esgotado",
                "O servidor demorou muito para responder.",
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text =
                "Erro ao realizar backup.";

            await DisplayAlert(
                "Erro no Backup",
                ex.Message,
                "OK");
        }
    }


    // =====================================================
    // ABRIR BACKUPS DO SERVIDOR
    // =====================================================

    // =====================================================
    // ENVIAR O ARQUIVO DO BANCO DE DADOS (amarv.db3) AO SERVIDOR
    // O servidor guarda em htdocs/amarv/backups.
    // =====================================================

    private async void Button_EnviarBanco_Clicked(
        object sender,
        EventArgs e)
    {
        string? temporario = null;

        try
        {
            if (!UsuarioAdministrador())
            {
                await ExibirAcessoNegado();
                return;
            }

            string origem = App.Database.CaminhoBanco;

            if (!File.Exists(origem))
            {
                throw new FileNotFoundException(
                    "O banco de dados local não foi encontrado.");
            }

            lbl_status.Text =
                "Preparando cópia do banco de dados...";

            // O banco pode estar em uso: envia uma cópia.
            temporario = Path.Combine(
                FileSystem.CacheDirectory,
                $"amarv_envio_{DateTime.Now:yyyyMMdd_HHmmss}.db3");

            using (var entrada = new FileStream(
                       origem,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var saida = new FileStream(
                       temporario,
                       FileMode.Create,
                       FileAccess.Write))
            {
                await entrada.CopyToAsync(saida);
            }

            lbl_status.Text =
                "Enviando o banco de dados para o servidor...";

            using HttpClient cliente =
                CriarCliente();

            cliente.Timeout =
                TimeSpan.FromMinutes(5);

            await using FileStream arquivo =
                File.OpenRead(temporario);

            using StreamContent conteudo =
                new StreamContent(arquivo);

            conteudo.Headers.ContentType =
                new System.Net.Http.Headers
                    .MediaTypeHeaderValue("application/octet-stream");

            HttpResponseMessage resposta =
                await cliente.PostAsync(
                    UrlEnviarBanco,
                    conteudo);

            string respostaTexto =
                await resposta.Content
                    .ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Erro HTTP: " +
                    (int)resposta.StatusCode +
                    "\n\n" +
                    respostaTexto);
            }

            var resultado =
                JsonSerializer.Deserialize<RespostaBackup>(
                    respostaTexto,
                    CriarOpcoesJson());

            if (resultado == null ||
                !resultado.sucesso)
            {
                throw new Exception(
                    resultado?.mensagem ??
                    "O servidor não confirmou o recebimento do banco.");
            }

            lbl_status.Text =
                "Banco de dados enviado com sucesso!\n" +
                "Arquivo: " +
                resultado.arquivo;

            await DisplayAlert(
                "Banco de dados",
                "O banco de dados do aplicativo foi salvo na pasta " +
                "backups do servidor.\n\nArquivo: " +
                resultado.arquivo,
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text =
                "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                "O aplicativo não conseguiu se conectar ao servidor.\n\n" +
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text =
                "Falha ao enviar o banco de dados.";

            await DisplayAlert(
                "Erro",
                ex.Message,
                "OK");
        }
        finally
        {
            if (temporario != null && File.Exists(temporario))
            {
                try { File.Delete(temporario); } catch { }
            }
        }
    }

    private async void Button_AbrirBackup_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            if (!UsuarioAdministrador())
            {
                await ExibirAcessoNegado();
                return;
            }

            lbl_status.Text =
                "Procurando backups no servidor...";

            using HttpClient cliente =
                CriarCliente();

            HttpResponseMessage resposta =
                await cliente.GetAsync(
                    UrlRestaurar);

            string respostaTexto =
                await resposta.Content
                    .ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Erro HTTP: " +
                    (int)resposta.StatusCode +
                    "\n\n" +
                    respostaTexto);
            }

            var resultado =
                JsonSerializer.Deserialize<RespostaListaBackups>(
                    respostaTexto,
                    CriarOpcoesJson());

            if (resultado == null ||
                !resultado.sucesso)
            {
                throw new Exception(
                    resultado?.mensagem ??
                    "Não foi possível listar os backups.");
            }

            if (resultado.backups == null ||
                resultado.backups.Count == 0)
            {
                await DisplayAlert(
                    "Backups",
                    "Nenhum backup foi encontrado no servidor.",
                    "OK");

                return;
            }

            var opcoes =
                resultado.backups
                    .Select(b =>
                        $"{b.arquivo}\n" +
                        $"Data: {b.data}")
                    .ToArray();

            string? escolha =
                await DisplayActionSheet(
                    "Escolha o backup para restaurar",
                    "Cancelar",
                    null,
                    opcoes);

            if (string.IsNullOrWhiteSpace(escolha) ||
                escolha == "Cancelar")
            {
                lbl_status.Text =
                    "Restauração cancelada.";

                return;
            }

            var backupEscolhido =
                resultado.backups
                    .FirstOrDefault(b =>
                        escolha.StartsWith(
                            b.arquivo,
                            StringComparison.OrdinalIgnoreCase));

            if (backupEscolhido == null)
            {
                throw new Exception(
                    "Não foi possível identificar o backup escolhido.");
            }

            bool confirmar =
                await DisplayAlert(
                    "Confirmar restauração",
                    "Você escolheu:\n\n" +
                    backupEscolhido.arquivo +
                    "\n\n" +
                    "Os dados desse backup serão restaurados.\n\n" +
                    "Deseja continuar?",
                    "Sim",
                    "Não");

            if (!confirmar)
            {
                lbl_status.Text =
                    "Restauração cancelada.";

                return;
            }

            await RestaurarBackupEscolhido(
                backupEscolhido.arquivo);
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text =
                "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                "Não foi possível acessar o servidor.\n\n" +
                ex.Message,
                "OK");
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert(
                "Tempo esgotado",
                "O servidor demorou muito para responder.",
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text =
                "Erro ao procurar backups.";

            await DisplayAlert(
                "Erro",
                ex.Message,
                "OK");
        }
    }
    // =====================================================
    // RESTAURAR BACKUP ESCOLHIDO
    // =====================================================

    private async Task RestaurarBackupEscolhido(
        string nomeArquivo)
    {
        try
        {
            if (!UsuarioAdministrador())
            {
                await ExibirAcessoNegado();
                return;
            }

            lbl_status.Text = "Baixando backup escolhido...";

            using HttpClient cliente = CriarCliente();

            string url =
                UrlRestaurar +
                "?arquivo=" +
                Uri.EscapeDataString(nomeArquivo);

            HttpResponseMessage resposta =
                await cliente.GetAsync(url);

            string respostaTexto =
                await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Erro HTTP: " +
                    (int)resposta.StatusCode +
                    "\n\n" +
                    respostaTexto);
            }

            var resultado =
                JsonSerializer.Deserialize<RespostaRestauracao>(
                    respostaTexto,
                    CriarOpcoesJson());

            if (resultado == null || !resultado.sucesso)
            {
                throw new Exception(
                    resultado?.mensagem ??
                    "Servidor não encontrou o backup.");
            }

            if (resultado.dados == null)
            {
                throw new Exception(
                    "O backup retornado está vazio.");
            }

            lbl_status.Text = "Backup encontrado. Restaurando dados...";

            // Carrega as pessoas já existentes no banco local.
            var pessoasLocais =
                await App.Database.Listar<Pessoa>();

            if (resultado.dados.pessoa == null)
            {
                throw new Exception(
                    "O backup não contém a lista de pessoas. " +
                    "Não é possível garantir o vínculo correto " +
                    "entre Pessoa e Paciente.");
            }

            lbl_status.Text =
                "Restaurando pessoas...";

            // 1. Restaurar Pessoa antes de Paciente.
            var pessoasParaRestaurar =
                PrepararPessoasRecebidas(
                    resultado.dados.pessoa,
                    pessoasLocais);

            if (pessoasParaRestaurar.Count > 0)
            {
                await App.Database.SubstituirDados(
                    pessoasParaRestaurar);
            }


            // 2. Restaurar pacientes preservando Pessoa.Id.
            if (resultado.dados.pacientes != null)
            {
                foreach (var paciente in resultado.dados.pacientes)
                {
                    Pessoa? pessoa =
                        pessoasLocais.FirstOrDefault(
                            p => p.Id == paciente.Id);

                    if (pessoa == null)
                    {
                        throw new Exception(
                            "Pessoa não encontrada para o paciente " +
                            paciente.Id +
                            ". A restauração foi interrompida para " +
                            "evitar um vínculo incorreto.");
                    }

                    await App.Database.SalvarPaciente(
                        paciente,
                        pessoa);
                }
            }

            // 3. Restaurar histórico depois dos pacientes.
            if (resultado.dados.historico != null)
            {
                foreach (var historico in resultado.dados.historico)
                {
                    await App.Database.SalvarHistoricoVacinal(
                        historico);
                }
            }

            lbl_status.Text =
                "Backup restaurado com sucesso!\n" +
                "Arquivo: " +
                resultado.arquivo;

            await DisplayAlert(
                "Restauração concluída",
                "O backup selecionado foi restaurado com sucesso.\n\n" +
                "Pessoas: " +
                resultado.dados.pessoa.Count +
                "\nPacientes: " +
                (resultado.dados.pacientes?.Count ?? 0) +
                "\nHistóricos: " +
                (resultado.dados.historico?.Count ?? 0) +
                "\n\nArquivo:\n" +
                resultado.arquivo,
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text = "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                ex.Message,
                "OK");
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert(
                "Tempo esgotado",
                "O servidor demorou muito para responder.",
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text = "Erro ao restaurar backup.";

            await DisplayAlert(
                "Erro na Restauração",
                ex.Message,
                "OK");
        }
    }

    // =====================================================
    // BOTÃO: ENVIAR
    // SQLITE → PHP → MYSQL
    // =====================================================

    private async void Button_Enviar_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            int enviados = await EnviarDadosParaServidor();

            lbl_status.Text =
                $"Servidor confirmou o processamento de {enviados} registros.";

            await DisplayAlert(
                "Envio concluído",
                $"O servidor processou {enviados} registros.",
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text =
                "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                "Não foi possível conectar ao servidor.\n\n" +
                ex.Message,
                "OK");
        }
        catch (TaskCanceledException)
        {
            lbl_status.Text =
                "Tempo de conexão esgotado.";

            await DisplayAlert(
                "Tempo esgotado",
                "O servidor demorou muito para responder.",
                "OK");
        }
        catch (JsonException ex)
        {
            lbl_status.Text =
                "Erro no JSON.";

            await DisplayAlert(
                "Erro nos dados",
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text =
                "Erro ao enviar dados.";

            await DisplayAlert(
                "Erro",
                ex.Message,
                "OK");
        }
    }


    // =====================================================
    // BOTÃO: RECUPERAR
    // MYSQL → PHP → MAUI → SQLITE
    // =====================================================

    private async void Button_Recuperar_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            await BaixarDadosDoServidor();

            await DisplayAlert(
                "Recuperação concluída",
                "Os dados do servidor foram recuperados e salvos no banco local.\n\n" +
                "Os usuários do servidor foram sincronizados.",
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text =
                "Falha na conexão.";

            await DisplayAlert(
                "Falha na conexão",
                "Não foi possível conectar ao servidor.\n\n" +
                ex.Message,
                "OK");
        }
        catch (TaskCanceledException)
        {
            lbl_status.Text =
                "Tempo de conexão esgotado.";

            await DisplayAlert(
                "Tempo esgotado",
                "O servidor demorou muito para responder.",
                "OK");
        }
        catch (JsonException ex)
        {
            lbl_status.Text =
                "Erro no JSON.";

            await DisplayAlert(
                "Erro nos dados",
                "O aplicativo não conseguiu interpretar os dados recebidos.\n\n" +
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text =
                "Erro ao recuperar dados.";

            await DisplayAlert(
                "Erro na recuperação",
                ex.Message,
                "OK");
        }
    }


    // =====================================================
    // PREPARAR PESSOAS RECEBIDAS DO SERVIDOR
    //
    // 1. Descarta registros sem Id válido (Id <= 0).
    // 2. O MySQL não guarda DataNascimento nem Nome_da_Mae da
    //    pessoa. Quando o servidor não envia esses campos, mantém
    //    o valor que já existe no banco local, em vez de gravar
    //    por cima com data 01/01/0001 e nome da mãe vazio.
    // =====================================================

    private static List<Pessoa> PrepararPessoasRecebidas(
        IEnumerable<Pessoa> recebidas,
        List<Pessoa> locais)
    {
        var resultado = new List<Pessoa>();

        foreach (var pessoa in recebidas)
        {
            if (pessoa.Id <= 0)
                continue;

            Pessoa? local =
                locais.FirstOrDefault(
                    l => l.Id == pessoa.Id);

            if (local != null)
            {
                if (pessoa.DataNascimento.Year <= 1)
                    pessoa.DataNascimento = local.DataNascimento;

                if (string.IsNullOrWhiteSpace(pessoa.Nome_da_Mae))
                    pessoa.Nome_da_Mae = local.Nome_da_Mae;
            }

            // Data de nascimento sem valor (01/01/0001) não é aceita no
            // cadastro de usuários. Usa o mesmo padrão 01/01/1900 que o
            // app já aplica às pessoas migradas sem data informada.
            if (pessoa.DataNascimento.Year <= 1)
                pessoa.DataNascimento = new DateTime(1900, 1, 1);

            resultado.Add(pessoa);
        }

        return resultado;
    }


    // =====================================================
    // MÉTODO DE ENVIO
    // SQLITE → PHP → MYSQL
    // =====================================================

    private async Task<int> EnviarDadosParaServidor()
    {
        lbl_status.Text =
            "Lendo todos os dados locais...";

        // Remove pessoas com Id 0, que o servidor recusa.
        await App.Database.RemoverPessoasSemId();

        var pessoa =
     await App.Database.Listar<Pessoa>();

        var pacientes =
            await App.Database.Listar<Paciente>();

        var usuarioDoSistema =
            await App.Database.Listar<UsuarioDoSistema>();

        foreach (var usuario in usuarioDoSistema)
        {
            bool pessoaExiste = pessoa.Any(
                p => p.Id == usuario.Id);

            if (usuario.Id <= 0 || !pessoaExiste)
            {
                throw new Exception(
                    $"Usuário inválido para sincronização.\n\n" +
                    $"ID do usuário: {usuario.Id}\n" +
                    $"Login: {usuario.Login}\n" +
                    $"Pessoa correspondente encontrada: {pessoaExiste}");
            }
        } // FIM DO FOREACH

        // A partir daqui, o restante do método fica fora do foreach.

        var administradorDoSistema =
            await App.Database.Listar<AdministradorDoSistema>();

        var sessoes =
                await App.Database.Listar<Sessao>();

            var condicoesClinicas =
                await App.Database.Listar<CondicaoClinica>();

            var vacinas =
                await App.Database.Listar<Vacina>();

            var doses =
                await App.Database.Listar<Dose>();

            var calendariosVacinais =
                await App.Database.Listar<CalendarioVacinal>();

            var calendariosSUS =
                await App.Database.Listar<CalendarioVacinalSUS>();

            var calendariosRedePrivada =
                await App.Database.Listar<CalendarioVacinalRedePrivada>();

            var regrasVacinais =
                await App.Database.Listar<RegraVacinal>();

            var historicoVacinal =
                await App.Database.Listar<HistoricoVacinal>();

            var analisesEsquemaVacinal =
                await App.Database.Listar<AnaliseEsquemaVacinal>();

            var relatorios =
                await App.Database.Listar<Relatorio>();

            var relatoriosVacinaisPaciente =
                await App.Database.Listar<RelatorioVacinalPaciente>();

            var relatoriosPacientesAtrasados =
                await App.Database.Listar<RelatorioPacienteComVacinasAtrasadas>();

            var relatoriosPacientesRegulares =
                await App.Database.Listar<RelatorioPacientesEmSituacaoRegular>();

            var sincronizacoes =
                await App.Database.Listar<Sincronizacao>();

            var backups =
                await App.Database.Listar<Backup>();

            int totalRegistros =
                pessoa.Count +
                pacientes.Count +
                usuarioDoSistema.Count +
                administradorDoSistema.Count +
                sessoes.Count +
                condicoesClinicas.Count +
                vacinas.Count +
                doses.Count +
                calendariosVacinais.Count +
                calendariosSUS.Count +
                calendariosRedePrivada.Count +
                regrasVacinais.Count +
                historicoVacinal.Count +
                analisesEsquemaVacinal.Count +
                relatorios.Count +
                relatoriosVacinaisPaciente.Count +
                relatoriosPacientesAtrasados.Count +
                relatoriosPacientesRegulares.Count +
                sincronizacoes.Count +
                backups.Count;

            lbl_status.Text =
                "Preparando " +
                totalRegistros +
                " registros para envio...";

            var dados = new
            {
                aplicativo = "Amar V",

                usuario = new
                {
                    nome = usuarioLogado.Nome,
                    perfil = usuarioLogado.Perfil
                },

                dataSincronizacao = DateTime.Now,

                pessoa = pessoa,

                pacientes = pacientes,

                usuarioDoSistema = usuarioDoSistema,

                administradorDoSistema =
                    administradorDoSistema,

                sessoes = sessoes,

                condicoesClinicas =
                    condicoesClinicas,

                vacinas = vacinas,

                doses = doses,

                calendariosVacinais =
                    calendariosVacinais,

                calendariosSUS =
                    calendariosSUS,

                calendariosRedePrivada =
                    calendariosRedePrivada,

                regrasVacinais =
                    regrasVacinais,

                historicoVacinal =
                    historicoVacinal,

                analisesEsquemaVacinal =
                    analisesEsquemaVacinal,

                relatorios =
                    relatorios,

                relatoriosVacinaisPaciente =
                    relatoriosVacinaisPaciente,

                relatoriosPacientesAtrasados =
                    relatoriosPacientesAtrasados,

                relatoriosPacientesRegulares =
                    relatoriosPacientesRegulares,

                sincronizacoes =
                    sincronizacoes,

                backups =
                    backups
            };

            string json =
                JsonSerializer.Serialize(
                    dados,
                    new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });

            lbl_status.Text =
                "Enviando " +
                totalRegistros +
                " registros para o servidor...";

            using HttpClient cliente =
                CriarCliente();

            using StringContent conteudo =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            HttpResponseMessage resposta =
                await cliente.PostAsync(
                    UrlSincronizacao,
                    conteudo);

            string respostaTexto =
                await resposta.Content
                    .ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Erro HTTP: " +
                    (int)resposta.StatusCode +
                    "\n\n" +
                    respostaTexto);
            }

            var resultado =
                JsonSerializer.Deserialize<RespostaSincronizacao>(
                    respostaTexto,
                    CriarOpcoesJson());


            if (resultado == null)
            {
                throw new Exception(
                    "O servidor retornou uma resposta vazia ou inválida.");
            }

            if (!resultado.sucesso)
            {
                throw new Exception(
                    resultado.mensagem ??
                    "O servidor não confirmou o envio.");
            }

            if (resultado.quantidadeRegistros <= 0)
            {
                throw new Exception(
                    "O servidor respondeu com sucesso, mas nenhum registro " +
                    "foi processado.\n\n" +
                    "Verifique as coleções enviadas pelo MAUI e o arquivo " +
                    "sincronizar.php.\n\n" +
                    "Resposta recebida:\n" +
                    $"Tabelas processadas: {resultado.quantidadeTabelas}\n" +
                    $"Registros ignorados: {resultado.quantidadeIgnorados}");
            }

            Sincronizacao sincronizacao =
                new Sincronizacao
                {
                    DataSincronizacao =
                        DateTime.Now,

                    Status =
                        "Dados enviados ao servidor",

                    QuantidadeRegistros =
                        totalRegistros
                };

            sincronizacao.SincronizarDados();

            await App.Database.SincronizacaoHelper
         .Salvar(sincronizacao);

            return resultado.quantidadeRegistros;
        }
    // =====================================================
    // BAIXAR DADOS DO SERVIDOR
    // MYSQL → PHP → MAUI → SQLITE
    // =====================================================

    private async Task BaixarDadosDoServidor()
    {
        _avisosRecuperacao.Clear();

        lbl_status.Text =
            "Baixando dados do servidor...";

        using HttpClient cliente =
            CriarCliente();

        HttpResponseMessage resposta =
            await cliente.GetAsync(
                UrlBaixarDados);

        string json =
            await resposta.Content
                .ReadAsStringAsync();

        if (!resposta.IsSuccessStatusCode)
        {
            throw new Exception(
                "Erro HTTP ao baixar dados: " +
                (int)resposta.StatusCode +
                "\n\n" +
                json);
        }

        lbl_status.Text =
            "Interpretando dados recebidos...";

        var resultado =
            JsonSerializer.Deserialize<RespostaDadosServidor>(
                json,
                CriarOpcoesJson());

        if (resultado == null)
        {
            throw new Exception(
                "O servidor não retornou dados válidos.");
        }

        if (!resultado.sucesso)
        {
            throw new Exception(
                resultado.mensagem ??
                "O servidor não enviou os dados.");
        }

        if (resultado.dados == null)
        {
            throw new Exception(
                "Nenhum dado foi recebido do servidor.");
        }

        var dados =
            resultado.dados;

        lbl_status.Text =
            "Atualizando banco local...";


        // =================================================
        // PESSOAS
        // =================================================

        if (dados.pessoa != null &&
            dados.pessoa.Count > 0)
        {
            var pessoasAntesDaRecuperacao =
                await App.Database.Listar<Pessoa>();

            var pessoasParaGravar =
                PrepararPessoasRecebidas(
                    dados.pessoa,
                    pessoasAntesDaRecuperacao);

            if (pessoasParaGravar.Count > 0)
            {
                await App.Database.SubstituirDados(
                    pessoasParaGravar);
            }
        }


        // =================================================
        // PACIENTES
        // Pessoa.Id deve ser igual a Paciente.Id.
        // =================================================

        // =================================================
        // PACIENTES
        // =================================================

        var pessoasLocais =
            await App.Database.Listar<Pessoa>();

        int pacientesImportados = 0;
        int pacientesIgnorados = 0;

        // Pacientes que não puderam ser gravados (ID + motivo).
        // Um paciente com cadastro incompleto não deve interromper
        // a recuperação de todos os outros dados.
        var pacientesComProblema = _avisosRecuperacao;

        if (dados.pacientes != null)
        {
            foreach (var paciente in dados.pacientes)
            {
                // Localiza a Pessoa vinculada ao Paciente pelo mesmo ID.
                Pessoa? pessoa =
                    pessoasLocais.FirstOrDefault(
                        p => p.Id == paciente.Id);

                if (pessoa == null)
                {
                    pacientesIgnorados++;
                    pacientesComProblema.Add(
                        "Paciente " + paciente.Id +
                        ": pessoa não encontrada.");
                    continue;
                }

                try
                {
                    await App.Database.SalvarPaciente(
                        paciente,
                        pessoa);

                    pacientesImportados++;
                }
                catch (Exception ex) when (
                    ex is ArgumentException ||
                    ex is InvalidOperationException)
                {
                    pacientesIgnorados++;
                    pacientesComProblema.Add(
                        "Paciente " + paciente.Id +
                        ": " + ex.Message);
                }
            }
        }
        // =================================================
        // USUÁRIOS DO SISTEMA
        // MYSQL → SQLITE
        // =================================================

        int usuariosRecebidos =
            await SincronizarUsuariosDoServidor(
                dados.usuarioDoSistema);


        // =================================================
        // ADMINISTRADORES
        // =================================================

        if (dados.administradorDoSistema != null &&
            dados.administradorDoSistema.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.administradorDoSistema);
        }


        // =================================================
        // SESSÕES
        // =================================================

        if (dados.sessoes != null &&
            dados.sessoes.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.sessoes);
        }


        // =================================================
        // CONDIÇÕES CLÍNICAS
        // =================================================

        if (dados.condicoesClinicas != null &&
            dados.condicoesClinicas.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.condicoesClinicas);
        }


        // =================================================
        // VACINAS
        // =================================================

        if (dados.vacinas != null &&
            dados.vacinas.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.vacinas);
        }


        // =================================================
        // DOSES
        // =================================================

        if (dados.doses != null &&
            dados.doses.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.doses);
        }


        // =================================================
        // CALENDÁRIO VACINAL
        // =================================================

        if (dados.calendariosVacinais != null &&
            dados.calendariosVacinais.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.calendariosVacinais);
        }


        // =================================================
        // CALENDÁRIO SUS
        // =================================================

        if (dados.calendariosSUS != null &&
            dados.calendariosSUS.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.calendariosSUS);
        }


        // =================================================
        // CALENDÁRIO REDE PRIVADA
        // =================================================

        if (dados.calendariosRedePrivada != null &&
            dados.calendariosRedePrivada.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.calendariosRedePrivada);
        }


        // =================================================
        // REGRAS VACINAIS
        // =================================================

        // O calendário vacinal é definido pela versão do aplicativo:
        // as regras do servidor NÃO são importadas (podem ser de uma
        // versão antiga e sobrescreveriam as regras atuais). Elas só
        // servem para religar o histórico às regras deste aparelho.


        // =================================================
        // HISTÓRICO VACINAL
        // =================================================

        if (dados.historicoVacinal != null &&
            dados.historicoVacinal.Count > 0)
        {
            await App.Database.RevincularHistoricoRecebidoAsync(
                dados.regrasVacinais,
                dados.historicoVacinal);

            await App.Database.SubstituirDados(
                dados.historicoVacinal);
        }


        // =================================================
        // ANÁLISES
        // =================================================

        if (dados.analisesEsquemaVacinal != null &&
            dados.analisesEsquemaVacinal.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.analisesEsquemaVacinal);
        }


        // =================================================
        // RELATÓRIOS
        // =================================================

        if (dados.relatorios != null &&
            dados.relatorios.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.relatorios);
        }


        // =================================================
        // RELATÓRIOS VACINAIS DO PACIENTE
        // =================================================

        if (dados.relatoriosVacinaisPaciente != null &&
            dados.relatoriosVacinaisPaciente.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.relatoriosVacinaisPaciente);
        }


        // =================================================
        // RELATÓRIOS DE PACIENTES ATRASADOS
        // =================================================

        if (dados.relatoriosPacientesAtrasados != null &&
            dados.relatoriosPacientesAtrasados.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.relatoriosPacientesAtrasados);
        }


        // =================================================
        // RELATÓRIOS DE PACIENTES REGULARES
        // =================================================

        if (dados.relatoriosPacientesRegulares != null &&
            dados.relatoriosPacientesRegulares.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.relatoriosPacientesRegulares);
        }


        // =================================================
        // SINCRONIZAÇÕES
        // =================================================

        if (dados.sincronizacoes != null &&
            dados.sincronizacoes.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.sincronizacoes);
        }


        // =================================================
        // BACKUPS
        // =================================================

        if (dados.backups != null &&
            dados.backups.Count > 0)
        {
            await App.Database.SubstituirDados(
                dados.backups);
        }


        // =================================================
        // GARANTIR USUÁRIOS INICIAIS
        // =================================================

        await GarantirUsuariosLogin();


        // =================================================
        // RESULTADO
        // =================================================

        lbl_status.Text =
            "Dados recuperados com sucesso!\n" +
            "Usuários: " +
            usuariosRecebidos +
            " | Pacientes: " +
            pacientesImportados + "/" +
            (dados.pacientes?.Count ?? 0) +
            " | Vacinas: " +
            (dados.vacinas?.Count ?? 0) +
            " | Histórico: " +
            (dados.historicoVacinal?.Count ?? 0);

        if (pacientesComProblema.Count > 0)
        {
            await DisplayAlert(
                "Atenção",
                "A recuperação foi concluída, mas " +
                pacientesComProblema.Count +
                " registro(s) não foram gravados:\n\n" +
                string.Join("\n", pacientesComProblema) +
                "\n\nCorrija o cadastro da pessoa (CPF com 11 " +
                "dígitos e data de nascimento válida) ou exclua " +
                "o cadastro incompleto.",
                "OK");
        }
    }


    // =====================================================
    // SINCRONIZAR USUÁRIOS DO MYSQL → SQLITE
    //
    // NÃO APAGA USUÁRIOS LOCAIS
    //
    // PROCURA PRIMEIRO PELO ID
    // DEPOIS PELO LOGIN
    //
    // =====================================================

    private async Task<int> SincronizarUsuariosDoServidor(
        List<UsuarioDoSistema>? usuariosServidor)
    {
        if (usuariosServidor == null ||
            usuariosServidor.Count == 0)
        {
            lbl_status.Text =
                "Nenhum usuário foi recebido do servidor.";

            return 0;
        }

        var usuariosLocais =
            await App.Database.ListarUsuarios();

        int inseridos = 0;
        int atualizados = 0;

        foreach (var usuarioServidor in usuariosServidor)
        {
            if (usuarioServidor == null)
                continue;


            // =================================================
            // PROCURAR PELO ID
            // =================================================

            UsuarioDoSistema? usuarioLocal = null;

            if (usuarioServidor.Id > 0)
            {
                usuarioLocal =
                    usuariosLocais.FirstOrDefault(
                        x => x.Id ==
                             usuarioServidor.Id);
            }


            // =================================================
            // SE NÃO ENCONTROU, PROCURAR PELO LOGIN
            // =================================================

            if (usuarioLocal == null &&
                !string.IsNullOrWhiteSpace(
                    usuarioServidor.Login))
            {
                usuarioLocal =
                    usuariosLocais.FirstOrDefault(
                        x =>
                            !string.IsNullOrWhiteSpace(
                                x.Login) &&
                            x.Login.Equals(
                                usuarioServidor.Login,
                                StringComparison.OrdinalIgnoreCase));
            }


            // =================================================
            // USUÁRIO JÁ EXISTE LOCALMENTE
            // =================================================

            if (usuarioLocal != null)
            {
                usuarioLocal.Id =
                    usuarioServidor.Id;

                usuarioLocal.Nome =
                    usuarioServidor.Nome;

                usuarioLocal.Login =
                    usuarioServidor.Login;

                // O servidor não envia mais o hash da senha:
                // só substitui a senha local se vier preenchida.
                if (!string.IsNullOrEmpty(
                        usuarioServidor.Senha))
                {
                    usuarioLocal.Senha =
                        usuarioServidor.Senha;
                }

                usuarioLocal.Perfil =
                    usuarioServidor.Perfil;

                usuarioLocal.Status =
                    usuarioServidor.Status;

                usuarioLocal.Ativo =
                    usuarioServidor.Ativo;

                try
                {
                    await App.Database.SalvarUsuario(
                        usuarioLocal);

                    atualizados++;
                }
                catch (Exception ex) when (
                    ex is ArgumentException ||
                    ex is InvalidOperationException)
                {
                    _avisosRecuperacao.Add(
                        "Usuário " + usuarioServidor.Id +
                        " (" + usuarioServidor.Login + "): " +
                        ex.Message);
                }
            }
            else
            {
                // =================================================
                // NOVO USUÁRIO DO SERVIDOR
                // =================================================

                if (string.IsNullOrEmpty(usuarioServidor.Senha))
                {
                    _avisosRecuperacao.Add(
                        "Usuário " + usuarioServidor.Id +
                        " (" + usuarioServidor.Login + "): " +
                        "veio do servidor sem senha. Defina uma " +
                        "senha neste aparelho para ele conseguir entrar.");
                }

                try
                {
                    await App.Database.SalvarUsuario(
                        usuarioServidor);

                    inseridos++;
                }
                catch (Exception ex) when (
                    ex is ArgumentException ||
                    ex is InvalidOperationException)
                {
                    _avisosRecuperacao.Add(
                        "Usuário " + usuarioServidor.Id +
                        " (" + usuarioServidor.Login + "): " +
                        ex.Message);
                }
            }
        }


        // =================================================
        // ATUALIZAR LISTA LOCAL
        // =================================================

        var usuariosDepois =
            await App.Database.ListarUsuarios();


        lbl_status.Text =
            "Usuários sincronizados.\n" +
            "Recebidos do servidor: " +
            usuariosServidor.Count +
            "\nAtualizados: " +
            atualizados +
            "\nNovos: " +
            inseridos +
            "\nTotal local: " +
            usuariosDepois.Count;

        return usuariosServidor.Count;
    }


    // =====================================================
    // GARANTIR USUÁRIOS DO LOGIN
    // =====================================================

    private async Task GarantirUsuariosLogin()
    {
        // Só cria o administrador provisório se não houver nenhum
        // usuário (a troca da senha é exigida no primeiro acesso).
        await App.Database.CarregarUsuariosIniciais();
    }


    private bool UsuarioAdministrador()
    {
        return usuarioLogado != null &&
               string.Equals(
                   usuarioLogado.Perfil,
                   "Administrador",
                   StringComparison.OrdinalIgnoreCase);
    }

    private Task ExibirAcessoNegado()
    {
        return DisplayAlert(
            "Acesso negado",
            "Somente o Administrador do Sistema pode realizar backup ou restaurar arquivos de backup.",
            "OK");
    }


    // =====================================================
    // VOLTAR AO MENU
    // =====================================================

    private async void Button_Menu_Clicked(
        object sender,
        EventArgs e)
    {
        await Navigation.PopAsync();
    }
}


// =========================================================
// CONVERSOR DE DATAS DO MYSQL
// =========================================================

public class MySqlDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formatos =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
    };


    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? valor =
            reader.GetString();

        if (string.IsNullOrWhiteSpace(valor))
            return DateTime.MinValue;


        if (DateTime.TryParseExact(
            valor,
            Formatos,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime data))
        {
            return data;
        }


        if (DateTime.TryParse(
            valor,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out data))
        {
            return data;
        }


        throw new JsonException(
            $"Não foi possível converter a data: {valor}");
    }


    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture));
    }
}


// =========================================================
// CONVERSOR DE BOOLEANO DO MYSQL
// =========================================================

public class MySqlBooleanConverter : JsonConverter<bool>
{
    public override bool Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType ==
            JsonTokenType.True)
        {
            return true;
        }


        if (reader.TokenType ==
            JsonTokenType.False)
        {
            return false;
        }


        if (reader.TokenType ==
            JsonTokenType.Number)
        {
            int valor =
                reader.GetInt32();

            return valor != 0;
        }


        if (reader.TokenType ==
            JsonTokenType.String)
        {
            string? valor =
                reader.GetString();

            if (string.IsNullOrWhiteSpace(valor))
                return false;


            if (valor == "1" ||
                valor.Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase) ||
                valor.Equals(
                    "sim",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }


            if (valor == "0" ||
                valor.Equals(
                    "false",
                    StringComparison.OrdinalIgnoreCase) ||
                valor.Equals(
                    "nao",
                    StringComparison.OrdinalIgnoreCase) ||
                valor.Equals(
                    "não",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }


        throw new JsonException(
            "Não foi possível converter o valor para Boolean.");
    }


    public override void Write(
        Utf8JsonWriter writer,
        bool value,
        JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}


// =========================================================
// RESPOSTA DO BACKUP
// =========================================================

public class RespostaBackup
{
    public bool sucesso { get; set; }

    public string mensagem { get; set; }

    public string arquivo { get; set; }
}


// =========================================================
// RESPOSTA DA LISTA DE BACKUPS
// =========================================================

public class RespostaListaBackups
{
    public bool sucesso { get; set; }

    public string mensagem { get; set; }

    public List<BackupServidor> backups { get; set; }
}


// =========================================================
// BACKUP ENCONTRADO NO SERVIDOR
// =========================================================

public class BackupServidor
{
    public string arquivo { get; set; }

    public string data { get; set; }

    public long tamanho { get; set; }
}


// =========================================================
// RESPOSTA DA RESTAURAÇÃO
// =========================================================

public class RespostaRestauracao
{
    public bool sucesso { get; set; }

    public string mensagem { get; set; }

    public string arquivo { get; set; }

    public DadosBackup dados { get; set; }
}


// =========================================================
// DADOS DO BACKUP
// =========================================================

public class DadosBackup
{
    public List<Pessoa> pessoa { get; set; } = new();

    public List<Paciente> pacientes { get; set; } = new();

    public List<HistoricoVacinal> historico { get; set; } = new();
}


// =========================================================
// RESPOSTA DA SINCRONIZAÇÃO
// =========================================================


public class RespostaSincronizacao
{
    public bool sucesso { get; set; }

    public string? mensagem { get; set; }

    public string? arquivo { get; set; }

    public int quantidadeRegistros { get; set; }

    public int quantidadeTabelas { get; set; }

    public int quantidadeIgnorados { get; set; }

    public int quantidadeAtualizados { get; set; }

    public int quantidadeInseridos { get; set; }
}


// =========================================================
// RESPOSTA DO DOWNLOAD DO SERVIDOR
// =========================================================

public class RespostaDadosServidor
{
    public bool sucesso { get; set; }

    public string mensagem { get; set; }

    public DadosServidor dados { get; set; }
}


// =========================================================
// DADOS RECEBIDOS DO SERVIDOR
// =========================================================

public class DadosServidor
{
    public List<Pessoa> pessoa { get; set; }

    public List<Paciente> pacientes { get; set; }

    public List<UsuarioDoSistema> usuarioDoSistema { get; set; }

    public List<AdministradorDoSistema> administradorDoSistema { get; set; }

    public List<Sessao> sessoes { get; set; }

    public List<CondicaoClinica> condicoesClinicas { get; set; }

    public List<Vacina> vacinas { get; set; }

    public List<Dose> doses { get; set; }

    public List<CalendarioVacinal> calendariosVacinais { get; set; }

    public List<CalendarioVacinalSUS> calendariosSUS { get; set; }

    public List<CalendarioVacinalRedePrivada> calendariosRedePrivada { get; set; }

    public List<RegraVacinal> regrasVacinais { get; set; }

    public List<HistoricoVacinal> historicoVacinal { get; set; }

    public List<AnaliseEsquemaVacinal> analisesEsquemaVacinal { get; set; }

    public List<Relatorio> relatorios { get; set; }

    public List<RelatorioVacinalPaciente> relatoriosVacinaisPaciente { get; set; }

    public List<RelatorioPacienteComVacinasAtrasadas> relatoriosPacientesAtrasados { get; set; }

    public List<RelatorioPacientesEmSituacaoRegular> relatoriosPacientesRegulares { get; set; }

    public List<Sincronizacao> sincronizacoes { get; set; }

    public List<Backup> backups { get; set; }
}