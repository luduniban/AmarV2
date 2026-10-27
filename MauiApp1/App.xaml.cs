using maui_amarv.Data;
using maui_amarv.Views;

namespace maui_amarv
{
    public partial class App : Application
    {
        private static BancoDados? database;

        // ============================================================
        // CAMINHO DO BANCO DE DADOS (ÚNICO PONTO DE CONFIGURAÇÃO)
        //
        // O aplicativo e a tela de Backup usam SEMPRE este caminho.
        // Para mudar o local do banco, altere somente a linha abaixo.
        //
        // Recomendado (padrão do projeto):
        //   Documentos do usuário\AmarV\amarv.db3
        //
        // Atual (mantido como você configurou):
        //   C:\xampp\htdocs\amarv\backups\AmarV_BancoApp.db3
        // ============================================================

        public static readonly string CaminhoBancoConfigurado =
            "";

        public static string CaminhoBanco
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CaminhoBancoConfigurado))
                    return CaminhoBancoConfigurado;

                // Pasta de dados do próprio aplicativo: funciona em
                // Windows, Android e iOS, em qualquer computador.
                return Path.Combine(
                    FileSystem.AppDataDirectory,
                    "amarv.db3");
            }
        }

        public static BancoDados Database
        {
            get
            {
                if (database == null)
                {
                    string caminho = CaminhoBanco;

                    string? pasta = Path.GetDirectoryName(caminho);

                    if (!string.IsNullOrEmpty(pasta) &&
                        !Directory.Exists(pasta))
                    {
                        Directory.CreateDirectory(pasta);
                    }

                    database = new BancoDados(caminho);
                }

                return database;
            }
        }

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(
            IActivationState activationState)
        {
            var window =
                new Window(
                    new NavigationPage(
                        new LoginPage()));

            window.Width = 420;
            window.Height = 800;

            return window;
        }
    }
}