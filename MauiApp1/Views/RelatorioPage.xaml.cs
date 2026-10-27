using System.Text;
using AmarV.Core.Relatorios;
using AmarV.Core.Resultado;
using maui_amarv.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using ServicoAnalise = maui_amarv.Services.AnaliseEsquemaVacinal;

namespace maui_amarv.Views;

// =====================================================================
// RELATÓRIOS (RF07, RF20 e RF21)
//
// Todos os relatórios usam a MESMA análise da tela de Análise Vacinal
// (motor AmarV.Core). Antes, os relatórios administrativos tinham um
// cálculo próprio (CalcularPendencias) que comparava 1 dose registrada
// com o total do esquema e contava regras de outros grupos (gestante,
// indígena, rede privada) — por isso quase ninguém aparecia como regular.
// =====================================================================
public partial class RelatoriosPage : ContentPage
{
    private readonly UsuarioDoSistema usuarioLogado;

    private string textoRelatorio = string.Empty;
    private string htmlRelatorio = string.Empty;
    private string csvRelatorio = string.Empty;

    private Dictionary<int, Pessoa> pessoasPorId = new();

    private sealed class PacienteExibicao
    {
        public Paciente Paciente { get; set; } = null!;
        public string Nome { get; set; } = string.Empty;
        public override string ToString() => Nome;
    }

    public RelatoriosPage(UsuarioDoSistema usuario)
    {
        InitializeComponent();
        usuarioLogado = usuario;
        ConfigurarTelaPorPerfil();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var pessoas = await App.Database.Listar<Pessoa>();
            pessoasPorId = pessoas.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

            var pacientes = await App.Database.ListarPacientes();

            // O Picker mostra o NOME (que fica na tabela Pessoa), não o objeto Paciente.
            pck_paciente.ItemsSource = pacientes
                .Select(p => new PacienteExibicao
                {
                    Paciente = p,
                    Nome = ObterPessoa(p.Id)?.Nome?.Trim() is { Length: > 0 } nome ? nome : $"Paciente ID {p.Id}"
                })
                .OrderBy(p => p.Nome)
                .ToList();
            pck_paciente.ItemDisplayBinding = new Binding(nameof(PacienteExibicao.Nome));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível carregar os dados: {ex.Message}", "OK");
        }
    }

    private Pessoa? ObterPessoa(int pacienteId)
    {
        pessoasPorId.TryGetValue(pacienteId, out Pessoa? pessoa);
        return pessoa;
    }

    private void ConfigurarTelaPorPerfil()
    {
        lbl_perfil.Text = "Perfil: " + usuarioLogado.Perfil;
        area_usuario.IsVisible = usuarioLogado.Perfil == "Usuario";
        area_admin.IsVisible = usuarioLogado.Perfil == "Administrador";
    }

    private bool EhAdministrador => usuarioLogado.Perfil == "Administrador";

    // =====================================================
    // FILTROS (rua, idade em anos, nome da mãe)
    // =====================================================

    private async Task<List<Paciente>> ObterPacientesFiltrados()
    {
        List<Paciente> pacientes = await App.Database.ListarPacientes();

        string filtroRua = entry_filtro_rua.Text?.Trim() ?? string.Empty;
        string filtroMae = entry_filtro_mae.Text?.Trim() ?? string.Empty;
        string filtroIdade = entry_filtro_idade.Text?.Trim() ?? string.Empty;

        if (filtroRua.Length > 0)
            pacientes = pacientes.Where(p =>
                ObterPessoa(p.Id)?.Endereco?.Contains(filtroRua, StringComparison.OrdinalIgnoreCase) == true).ToList();

        if (filtroMae.Length > 0)
            pacientes = pacientes.Where(p =>
                ObterPessoa(p.Id)?.Nome_da_Mae?.Contains(filtroMae, StringComparison.OrdinalIgnoreCase) == true).ToList();

        if (filtroIdade.Length > 0)
        {
            if (!int.TryParse(filtroIdade, out int idade))
            {
                await DisplayAlert("Filtro inválido", "Informe a idade em anos (somente números).", "OK");
                return new List<Paciente>();
            }
            pacientes = pacientes.Where(p => p.IdadeAnos == idade).ToList();
        }

        return pacientes;
    }

    private string DescreverFiltros()
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry_filtro_rua.Text)) partes.Add($"rua \"{entry_filtro_rua.Text.Trim()}\"");
        if (!string.IsNullOrWhiteSpace(entry_filtro_idade.Text)) partes.Add($"idade {entry_filtro_idade.Text.Trim()} anos");
        if (!string.IsNullOrWhiteSpace(entry_filtro_mae.Text)) partes.Add($"mãe \"{entry_filtro_mae.Text.Trim()}\"");
        return partes.Count == 0 ? "sem filtros" : "filtros: " + string.Join(", ", partes);
    }

    // =====================================================
    // RF07 — RELATÓRIO INDIVIDUAL DO PACIENTE
    // =====================================================

    private async void Button_RelatorioPaciente_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (pck_paciente.SelectedItem is not PacienteExibicao selecionado)
            {
                await DisplayAlert("Atenção", "Selecione um paciente.", "OK");
                return;
            }

            Paciente paciente = selecionado.Paciente;
            Pessoa? pessoa = ObterPessoa(paciente.Id);
            if (pessoa == null)
            {
                await DisplayAlert("Atenção", "Não foi encontrada a pessoa vinculada a este paciente.", "OK");
                return;
            }

            var servico = new ServicoAnalise(App.Database);
            ResultadoAnalise analise = await servico.AnalisarAsync(paciente, pessoa: pessoa);

            textoRelatorio =
                "RELATÓRIO DE SITUAÇÃO VACINAL DO PACIENTE\n\n" +
                $"Nome: {pessoa.Nome}\n" +
                $"Nome da mãe: {pessoa.Nome_da_Mae}\n" +
                $"Endereço: {pessoa.Endereco}\n" +
                $"Telefone: {pessoa.Telefone}\n" +
                $"Cartão SUS: {paciente.CartaoSUS}\n\n" +
                FormatadorTexto.Gerar(analise);

            htmlRelatorio = FormatadorHtml.Gerar(analise);
            csvRelatorio = RelatorioConsolidado.Csv(
                new[] { new PacienteRelatorio { Analise = analise, NomeMae = pessoa.Nome_da_Mae, Telefone = pessoa.Telefone, Endereco = pessoa.Endereco } },
                TipoRelatorio.Geral);

            lbl_relatorio.Text = textoRelatorio;

            var relatorio = new RelatorioVacinalPaciente();
            relatorio.GerarRelatorioVacinalPaciente(paciente, pessoa);
            relatorio.Conteudo = textoRelatorio;
            relatorio.StatusVacinal = analise.SituacaoTexto;
            relatorio.Periodo = analise.DataAnalise.ToString("dd/MM/yyyy");
            await App.Database.SalvarRelatorioVacinalPaciente(relatorio);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    // =====================================================
    // RF20, RF21 E GERAL — RELATÓRIOS ADMINISTRATIVOS
    // =====================================================

    private async Task<List<PacienteRelatorio>?> AnalisarFiltradosAsync()
    {
        if (!EhAdministrador)
        {
            await DisplayAlert("Acesso negado", "Relatório administrativo permitido apenas para administrador.", "OK");
            return null;
        }

        lbl_relatorio.Text = "Analisando pacientes...";
        var pacientes = await ObterPacientesFiltrados();
        var servico = new ServicoAnalise(App.Database);
        return await servico.AnalisarTodosAsync(pacientes);
    }

    private async Task GerarAdministrativoAsync(TipoRelatorio tipo)
    {
        try
        {
            var analisados = await AnalisarFiltradosAsync();
            if (analisados == null) return;

            DateTime agora = DateTime.Now;
            textoRelatorio = $"({DescreverFiltros()})\n" + RelatorioConsolidado.Texto(analisados, tipo, agora);
            htmlRelatorio = RelatorioConsolidado.Html(analisados, tipo, agora);
            csvRelatorio = RelatorioConsolidado.Csv(analisados, tipo);
            lbl_relatorio.Text = textoRelatorio;

            int quantidade = RelatorioConsolidado.Filtrar(analisados, tipo).Count;

            switch (tipo)
            {
                case TipoRelatorio.PacientesEmAtraso:
                    var atrasados = new RelatorioPacienteComVacinasAtrasadas();
                    atrasados.GerarRelatorioPacientesAtrasados(quantidade);
                    atrasados.Conteudo = textoRelatorio;
                    await App.Database.SalvarRelatorioPacientesAtrasados(atrasados);
                    break;

                case TipoRelatorio.PacientesRegulares:
                    var regulares = new RelatorioPacientesEmSituacaoRegular();
                    regulares.GerarRelatorioSituacaoRegular(quantidade);
                    regulares.Conteudo = textoRelatorio;
                    await App.Database.SalvarRelatorioPacientesRegulares(regulares);
                    break;

                default:
                    await App.Database.SalvarRelatorio(new Relatorio
                    {
                        DataGeracao = agora,
                        Periodo = "Geral",
                        Conteudo = textoRelatorio
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private async void Button_Pendentes_Clicked(object sender, EventArgs e) =>
        await GerarAdministrativoAsync(TipoRelatorio.PacientesEmAtraso);

    private async void Button_Regulares_Clicked(object sender, EventArgs e) =>
        await GerarAdministrativoAsync(TipoRelatorio.PacientesRegulares);

    private async void Button_Geral_Clicked(object sender, EventArgs e) =>
        await GerarAdministrativoAsync(TipoRelatorio.Geral);

    // =====================================================
    // EXPORTAR / COMPARTILHAR / IMPRIMIR
    // =====================================================

    private async Task CompartilharArquivoAsync(string conteudo, string extensao, string tipoMime, string titulo)
    {
        // Pasta interna do app: funciona no Android, iOS e Windows.
        string pasta = Path.Combine(FileSystem.CacheDirectory, "relatorios");
        Directory.CreateDirectory(pasta);
        string caminho = Path.Combine(pasta, $"relatorio_amarv_{DateTime.Now:yyyyMMdd_HHmmss}.{extensao}");

        // CSV com BOM para o Excel reconhecer acentos.
        File.WriteAllText(caminho, conteudo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: extensao == "csv"));

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = titulo,
            File = new ShareFile(caminho, tipoMime)
        });
    }

    private async void Button_Excel_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(csvRelatorio))
            {
                await DisplayAlert("Atenção", "Gere um relatório primeiro.", "OK");
                return;
            }
            await CompartilharArquivoAsync(csvRelatorio, "csv", "text/csv", "Relatório Amar V (CSV)");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private async void Button_Compartilhar_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(textoRelatorio))
        {
            await DisplayAlert("Atenção", "Gere um relatório primeiro.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Relatório Amar V",
            Text = textoRelatorio
        });
    }

    private async void Button_Imprimir_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlRelatorio))
            {
                await DisplayAlert("Atenção", "Gere um relatório primeiro.", "OK");
                return;
            }
            // O HTML abre no navegador; use "Imprimir" ou "Salvar como PDF".
            await CompartilharArquivoAsync(htmlRelatorio, "html", "text/html", "Relatório Amar V (HTML)");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private async void Button_Menu_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
