using AmarV.Core.Relatorios;
using Microsoft.Maui.Controls.Shapes;
using ServicoAnalise = maui_amarv.Services.AnaliseEsquemaVacinal;

namespace maui_amarv.Views;

// =====================================================================
// CALENDÁRIO VACINAL (RF05) — lido das regras cadastradas.
// Filtros: calendário (SUS / Rede Privada) e grupo (Criança, Adulto...).
// =====================================================================
public partial class CalendarioVacinal : ContentPage
{
    private List<LinhaCalendario> linhas = new();
    private bool carregando;

    public CalendarioVacinal()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            carregando = true;
            var regras = await App.Database.ListarRegrasVacinais();
            linhas = CalendarioResumo.Montar(regras.Select(ServicoAnalise.Converter));

            pck_calendario.ItemsSource = new List<string> { "SUS", "Rede Privada" };
            pck_calendario.SelectedIndex = 0;

            var grupos = new List<string> { "Todos os grupos" };
            grupos.AddRange(CalendarioResumo.OrdemGrupos.Where(g => linhas.Any(l => l.GrupoEtario == g)));
            pck_grupo.ItemsSource = grupos;
            pck_grupo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível carregar o calendário.\n\n{ex.Message}", "OK");
        }
        finally
        {
            carregando = false;
        }

        Exibir();
    }

    private void Filtro_Alterado(object sender, EventArgs e)
    {
        if (!carregando) Exibir();
    }

    private void Exibir()
    {
        pnl_calendario.Children.Clear();

        string calendario = pck_calendario.SelectedItem as string ?? "SUS";
        string grupo = pck_grupo.SelectedItem as string ?? "Todos os grupos";

        var filtradas = linhas
            .Where(l => l.Calendario == calendario)
            .Where(l => grupo == "Todos os grupos" || l.GrupoEtario == grupo)
            .ToList();

        if (filtradas.Count == 0)
        {
            pnl_calendario.Children.Add(new Label
            {
                Text = "Nenhuma regra cadastrada para este filtro. O administrador pode cadastrar em \"Atualizar Vacinas e Doses\".",
                TextColor = Color.FromArgb("#475467")
            });
            return;
        }

        string grupoAtual = string.Empty;
        foreach (var l in filtradas)
        {
            if (l.GrupoEtario != grupoAtual)
            {
                grupoAtual = l.GrupoEtario;
                pnl_calendario.Children.Add(new Label
                {
                    Text = grupoAtual,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1F3C88"),
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            var corpo = new VerticalStackLayout { Spacing = 2 };
            corpo.Children.Add(new Label
            {
                Text = l.Vacina + (l.GrupoEspecial.Length > 0 ? $"  ·  {l.GrupoEspecial}" : string.Empty),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            });
            corpo.Children.Add(Linha($"{l.Esquema}{(l.Via.Length > 0 ? $" · via {l.Via}" : "")}"));
            corpo.Children.Add(Linha($"Faixa: {l.Faixa}"));
            if (!string.IsNullOrWhiteSpace(l.Indicacao)) corpo.Children.Add(Linha(l.Indicacao));
            if (!string.IsNullOrWhiteSpace(l.Observacao)) corpo.Children.Add(Linha("Obs.: " + l.Observacao));

            pnl_calendario.Children.Add(new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#D0D5DD"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 8),
                Content = corpo
            });
        }
    }

    private static Label Linha(string texto) => new()
    {
        Text = texto,
        FontSize = 13,
        TextColor = Color.FromArgb("#344054"),
        LineBreakMode = LineBreakMode.WordWrap
    };

    private async void Button_Voltar_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void Button_Menu_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
