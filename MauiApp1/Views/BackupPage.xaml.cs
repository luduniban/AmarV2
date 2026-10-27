using maui_amarv.Helpers;
using maui_amarv.Models;

namespace maui_amarv.Views;

public partial class BackupPage : ContentPage
{
    private readonly UsuarioDoSistema usuarioLogado;

    public BackupPage(UsuarioDoSistema usuario)
    {
        InitializeComponent();

        usuarioLogado = usuario;

        lbl_usuario.Text =
            $"Usuário: {usuarioLogado.Nome} | Perfil: {usuarioLogado.Perfil}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        bool administrador =
            usuarioLogado?.Perfil?.Trim().Equals(
                "Administrador",
                StringComparison.OrdinalIgnoreCase) == true;

        btn_backup.IsEnabled = administrador;
        btn_recuperar.IsEnabled = administrador;

        txt_ip_servidor.IsEnabled = administrador;
        txt_token_servidor.IsEnabled = administrador;
        btn_salvar_servidor.IsEnabled = administrador;
        btn_testar_servidor.IsEnabled = administrador;

        // Mostra o IP salvo; o token nunca é exibido.
        txt_ip_servidor.Text = ServidorBackupHelper.IpAtual;

        txt_token_servidor.Placeholder =
            ServidorBackupHelper.TemToken
                ? "Token salvo (deixe em branco para manter)"
                : "Cole aqui o token";

        if (!administrador)
        {
            lbl_status.Text =
                "Backup disponível somente para o administrador.";
        }
    }

    // ============================================================
    // SALVAR / TESTAR IP E TOKEN DO SERVIDOR
    // ============================================================

    private async Task<bool> SalvarConfiguracaoServidorAsync()
    {
        string ip = txt_ip_servidor.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(ip))
        {
            await DisplayAlert(
                "IP inválido",
                "Digite o IP do servidor.",
                "OK");

            return false;
        }

        bool ipValido =
            System.Net.IPAddress.TryParse(ip, out _) ||
            Uri.CheckHostName(ip) == UriHostNameType.Dns;

        if (!ipValido)
        {
            await DisplayAlert(
                "IP inválido",
                "Digite um endereço válido.\n\nExemplo:\n192.168.88.254",
                "OK");

            return false;
        }

        ServidorBackupHelper.SalvarConfiguracao(
            ip,
            txt_token_servidor.Text);

        if (!string.IsNullOrWhiteSpace(txt_token_servidor.Text))
        {
            txt_token_servidor.Text = string.Empty;
            txt_token_servidor.Placeholder =
                "Token salvo (deixe em branco para manter)";
        }

        return true;
    }

    private async void Button_SalvarServidor_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            if (!await SalvarConfiguracaoServidorAsync())
                return;

            lbl_status.Text =
                $"Servidor salvo: {ServidorBackupHelper.IpAtual}" +
                (ServidorBackupHelper.TemToken
                    ? "\nToken configurado."
                    : "\nAtenção: nenhum token foi informado.");

            await DisplayAlert(
                "Servidor",
                "IP e token salvos com sucesso.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Erro",
                "Não foi possível salvar.\n\n" + ex.Message,
                "OK");
        }
    }

    private async void Button_TestarServidor_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            if (!await SalvarConfiguracaoServidorAsync())
                return;

            lbl_status.Text = "Testando a conexão com o servidor...";

            var bancos =
                await ServidorBackupHelper.ListarBancosAsync();

            lbl_status.Text =
                $"Conexão OK com {ServidorBackupHelper.IpAtual}.\n" +
                $"Backups no servidor: {bancos.Count}.";

            await DisplayAlert(
                "Conexão",
                $"Conexão com o servidor realizada com sucesso.\n\n" +
                $"Backups encontrados: {bancos.Count}.",
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text = "Falha ao conectar ao servidor.";

            await DisplayAlert(
                "Falha na conexão",
                "Não foi possível usar o servidor com o IP e o token " +
                "informados.\n\n" + ex.Message +
                "\n\nConfira o IP, o token e se o Apache está ligado.",
                "OK");
        }
    }

    // ============================================================
    // REALIZAR BACKUP
    // O backup é enviado ao servidor (IP configurado na tela de
    // sincronização), como a sincronização. Se o servidor não estiver
    // acessível, a cópia local é mantida em Documentos > AmarV.
    // ============================================================

    private async void Button_Backup_Clicked(object sender, EventArgs e)
    {
        try
        {
            bool administrador =
                usuarioLogado?.Perfil?.Trim().Equals(
                    "Administrador",
                    StringComparison.OrdinalIgnoreCase) == true;

            if (!administrador)
            {
                await DisplayAlert(
                    "Acesso negado",
                    "Somente o administrador pode realizar backup.",
                    "OK");

                return;
            }

            string pasta = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "AmarV");

            if (!Directory.Exists(pasta))
            {
                Directory.CreateDirectory(pasta);
            }

            // Banco realmente usado pelo aplicativo (App.xaml.cs).
            string banco = App.CaminhoBanco;

            if (!File.Exists(banco))
            {
                await DisplayAlert(
                    "Erro",
                    "Banco de dados não encontrado.",
                    "OK");

                return;
            }

            string nomeBackup =
                $"backup_amarv_{DateTime.Now:yyyyMMdd_HHmmss}.db3";

            string caminhoBackup =
                Path.Combine(
                    pasta,
                    nomeBackup);

            // ====================================================
            // CRIA A CÓPIA DO BANCO (o banco pode estar em uso)
            // ====================================================

            using (var entrada = new FileStream(
                       banco,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var saida = new FileStream(
                       caminhoBackup,
                       FileMode.Create,
                       FileAccess.Write))
            {
                await entrada.CopyToAsync(saida);
            }

            // ====================================================
            // ENVIA PARA O SERVIDOR
            // ====================================================

            lbl_status.Text =
                "Enviando o backup para o servidor...";

            string? arquivoNoServidor = null;
            string? erroServidor = null;

            try
            {
                arquivoNoServidor =
                    await ServidorBackupHelper.EnviarBancoAsync(
                        caminhoBackup);
            }
            catch (Exception exServidor)
            {
                erroServidor = exServidor.Message;
            }

            // ====================================================
            // CRIA O REGISTRO DO BACKUP
            // ====================================================

            Backup backup = new Backup
            {
                DataBackup = DateTime.Now,
                Status = arquivoNoServidor != null
                    ? "Backup enviado ao servidor"
                    : "Backup local (servidor indisponível)",
                LocalArquivo = arquivoNoServidor != null
                    ? $"Servidor: {arquivoNoServidor}"
                    : caminhoBackup
            };

            // Executa a rotina existente da classe Backup
            backup.RealizarBackup();

            backup.Status = arquivoNoServidor != null
                ? "Backup enviado ao servidor"
                : "Backup local (servidor indisponível)";

            backup.LocalArquivo = arquivoNoServidor != null
                ? $"Servidor: {arquivoNoServidor}"
                : caminhoBackup;

            await App.Database.BackupHelper.Salvar(backup);

            // Enviado com sucesso: não mantém cópia duplicada no aparelho.
            if (arquivoNoServidor != null && File.Exists(caminhoBackup))
            {
                try { File.Delete(caminhoBackup); } catch { }
            }

            // ====================================================
            // ATUALIZA O STATUS NA TELA
            // ====================================================

            if (arquivoNoServidor != null)
            {
                lbl_status.Text =
                    $"Último backup: {backup.DataBackup:dd/MM/yyyy HH:mm}" +
                    $"\nSalvo no servidor: {arquivoNoServidor}";

                await DisplayAlert(
                    "Backup realizado",
                    "Backup salvo com sucesso no servidor.\n\n" +
                    $"Data: {backup.DataBackup:dd/MM/yyyy}\n" +
                    $"Horário: {backup.DataBackup:HH:mm}\n\n" +
                    $"Arquivo: {arquivoNoServidor}",
                    "OK");
            }
            else
            {
                lbl_status.Text =
                    $"Último backup: {backup.DataBackup:dd/MM/yyyy HH:mm}" +
                    $"\nSomente local: {nomeBackup}";

                await DisplayAlert(
                    "Backup salvo apenas no aparelho",
                    "Não foi possível enviar o backup ao servidor.\n\n" +
                    $"{erroServidor}\n\n" +
                    "A cópia foi mantida em Documentos > AmarV. " +
                    "Confira o IP e o token na tela de sincronização e " +
                    "faça o backup novamente quando o servidor estiver disponível.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Erro no Backup",
                $"Não foi possível realizar o backup.\n\n" +
                ex.Message,
                "OK");
        }
    }

    // ============================================================
    // RECUPERAR BACKUP
    // Lista os backups do servidor e os locais (do aparelho).
    // ============================================================

    private class OpcaoBackup
    {
        public string Rotulo { get; set; } = "";
        public bool NoServidor { get; set; }
        public string Nome { get; set; } = "";
        public string Caminho { get; set; } = "";
        public DateTime Data { get; set; }
    }

    private async void Button_Recuperar_Clicked(object sender, EventArgs e)
    {
        string? temporario = null;

        try
        {
            bool administrador =
                usuarioLogado?.Perfil?.Trim().Equals(
                    "Administrador",
                    StringComparison.OrdinalIgnoreCase) == true;

            if (!administrador)
            {
                await DisplayAlert(
                    "Acesso negado",
                    "Somente o administrador pode recuperar um backup.",
                    "OK");

                return;
            }

            string pasta = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "AmarV");

            if (!Directory.Exists(pasta))
            {
                Directory.CreateDirectory(pasta);
            }

            var opcoes = new List<OpcaoBackup>();
            string? avisoServidor = null;

            // ====================================================
            // BACKUPS DO SERVIDOR
            // ====================================================

            lbl_status.Text = "Procurando backups no servidor...";

            try
            {
                var doServidor =
                    await ServidorBackupHelper.ListarBancosAsync();

                foreach (var item in doServidor)
                {
                    opcoes.Add(new OpcaoBackup
                    {
                        NoServidor = true,
                        Nome = item.nome,
                        Data = item.DataHora,
                        Rotulo =
                            $"Servidor — {item.DataHora:dd/MM/yyyy} às " +
                            $"{item.DataHora:HH:mm}"
                    });
                }
            }
            catch (Exception exServidor)
            {
                avisoServidor = exServidor.Message;
            }

            // ====================================================
            // BACKUPS LOCAIS
            // ====================================================

            foreach (string arquivo in Directory.GetFiles(
                         pasta,
                         "backup_amarv_*.db3"))
            {
                DateTime dataBackup =
                    File.GetLastWriteTime(arquivo);

                opcoes.Add(new OpcaoBackup
                {
                    NoServidor = false,
                    Caminho = arquivo,
                    Data = dataBackup,
                    Rotulo =
                        $"Aparelho — {dataBackup:dd/MM/yyyy} às " +
                        $"{dataBackup:HH:mm}"
                });
            }

            lbl_status.Text = string.Empty;

            if (opcoes.Count == 0)
            {
                await DisplayAlert(
                    "Backup",
                    avisoServidor != null
                        ? "Nenhum backup encontrado.\n\nNão foi possível " +
                          $"consultar o servidor:\n{avisoServidor}"
                        : "Nenhum backup encontrado.",
                    "OK");

                return;
            }

            // Mais recente primeiro (servidor e aparelho misturados).
            opcoes = opcoes
                .OrderByDescending(o => o.Data)
                .ToList();

            // ====================================================
            // MOSTRA OS BACKUPS PARA O ADMINISTRADOR
            // ====================================================

            string titulo = avisoServidor != null
                ? "Servidor indisponível: só backups do aparelho"
                : "Selecione o backup para recuperar";

            string backupSelecionado =
                await DisplayActionSheet(
                    titulo,
                    "Cancelar",
                    null,
                    opcoes.Select(o => o.Rotulo).ToArray());

            if (string.IsNullOrWhiteSpace(backupSelecionado) ||
                backupSelecionado == "Cancelar")
            {
                return;
            }

            OpcaoBackup? escolhido =
                opcoes.FirstOrDefault(o => o.Rotulo == backupSelecionado);

            if (escolhido == null)
            {
                await DisplayAlert(
                    "Erro",
                    "Não foi possível identificar o backup selecionado.",
                    "OK");

                return;
            }

            // ====================================================
            // CONFIRMA A RESTAURAÇÃO
            // ====================================================

            bool confirmar =
                await DisplayAlert(
                    "Confirmar recuperação",
                    $"Deseja restaurar o backup de:\n\n" +
                    $"{backupSelecionado}\n\n" +
                    "Os dados atuais do aplicativo serão substituídos.",
                    "Restaurar",
                    "Cancelar");

            if (!confirmar)
            {
                return;
            }

            // ====================================================
            // OBTÉM O ARQUIVO (baixa do servidor, se for o caso)
            // ====================================================

            string caminhoBackup = escolhido.Caminho;

            if (escolhido.NoServidor)
            {
                lbl_status.Text =
                    "Baixando o backup do servidor...";

                temporario = Path.Combine(
                    FileSystem.CacheDirectory,
                    $"amarv_recuperacao_{DateTime.Now:yyyyMMdd_HHmmss}.db3");

                await ServidorBackupHelper.BaixarBancoAsync(
                    escolhido.Nome,
                    temporario);

                caminhoBackup = temporario;
            }

            // Banco realmente usado pelo aplicativo (App.xaml.cs).
            string banco = App.CaminhoBanco;

            // ====================================================
            // FAZ BACKUP DO BANCO ATUAL
            // ANTES DA RESTAURAÇÃO
            // ====================================================

            if (File.Exists(banco))
            {
                string nomeBackupSeguranca =
                    $"backup_antes_restauracao_{DateTime.Now:yyyyMMdd_HHmmss}.db3";

                string caminhoBackupSeguranca =
                    Path.Combine(
                        pasta,
                        nomeBackupSeguranca);

                using (var entrada = new FileStream(
                           banco,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (var saida = new FileStream(
                           caminhoBackupSeguranca,
                           FileMode.Create,
                           FileAccess.Write))
                {
                    await entrada.CopyToAsync(saida);
                }
            }

            // ====================================================
            // RESTAURA O BACKUP SELECIONADO
            // Fecha a conexão com o banco antes de trocar o arquivo.
            // ====================================================

            await App.Database.FecharConexaoAsync();

            File.Copy(
                caminhoBackup,
                banco,
                true);

            // ====================================================
            // ATUALIZA O STATUS
            // ====================================================

            lbl_status.Text =
                $"Backup recuperado:\n{backupSelecionado}";

            await DisplayAlert(
                "Backup recuperado",
                "O backup foi restaurado com sucesso.\n\n" +
                "É recomendado fechar e abrir novamente o aplicativo " +
                "para que todos os dados restaurados sejam carregados.",
                "OK");
        }
        catch (HttpRequestException ex)
        {
            lbl_status.Text = string.Empty;

            await DisplayAlert(
                "Falha na conexão",
                "O aplicativo não conseguiu se conectar ao servidor.\n\n" +
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            lbl_status.Text = string.Empty;

            await DisplayAlert(
                "Erro na recuperação",
                $"Não foi possível recuperar o backup.\n\n" +
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

    // ============================================================
    // VOLTAR AO MENU
    // ============================================================

    private async void Button_Menu_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}