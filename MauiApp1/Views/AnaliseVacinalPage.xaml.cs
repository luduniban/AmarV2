using System.Text;
using AmarV.Core.Relatorios;
using AmarV.Core.Resultado;
using maui_amarv.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using ServicoAnalise = maui_amarv.Services.AnaliseEsquemaVacinal;

namespace maui_amarv.Views;

// =====================================================================
// ANÁLISE VACINAL (RF08 a RF14)
//
// 1. O profissional escolhe o paciente.
// 2. O serviço chama o motor AmarV.Core, que devolve o resultado em partes.
// 3. A tela mostra cartões coloridos por situação e o plano de recuperação.
// 4. A análise é gravada com as colunas estruturadas (para relatórios e servidor).
// 5. Em cada vacina indicada para hoje há o botão "Registrar dose aplicada hoje",
//    que grava a dose no histórico e refaz a análise na hora (RF03).
// =====================================================================
public partial class AnaliseVacinalPage : ContentPage
{
    private bool carregandoPacientes;
    private ResultadoAnalise? resultadoAtual;
    private PacienteExibicao? selecionadoAtual;

    // Cores das situações (as mesmas do relatório HTML)
    private static readonly Color CorAtrasada = Color.FromArgb("#B42318");
    private static readonly Color CorHoje = Color.FromArgb("#B54708");
    private static readonly Color CorProxima = Color.FromArgb("#475467");
    private static readonly Color CorEmDia = Color.FromArgb("#2B7A78");
    private static readonly Color CorRestricao = Color.FromArgb("#6941C6");
    private static readonly Color CorFora = Color.FromArgb("#98A2B3");
    private static readonly Color CorTitulo = Color.FromArgb("#1F3C88");
    private static readonly Color CorTexto = Color.FromArgb("#344054");

    private sealed class PacienteExibicao
    {
        public Paciente Paciente { get; set; } = null!;
        public Pessoa? Pessoa { get; set; }
        public string Nome { get; set; } = string.Empty;
        public override string ToString() => Nome;
    }

    public AnaliseVacinalPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarPacientes();
    }

    // =====================================================
    // CARREGAR PACIENTES
    // =====================================================

    private async Task CarregarPacientes()
    {
        try
        {
            carregandoPacientes = true;
            int? idSelecionado = selecionadoAtual?.Paciente.Id;

            var pacientes = await App.Database.Listar<Paciente>();
            var pessoas = await App.Database.Listar<Pessoa>();

            var lista = pacientes
                .Select(p =>
                {
                    var pessoa = pessoas.FirstOrDefault(x => x.Id == p.Id);
                    return new PacienteExibicao
                    {
                        Paciente = p,
                        Pessoa = pessoa,
                        Nome = !string.IsNullOrWhiteSpace(pessoa?.Nome) ? pessoa!.Nome.Trim() : $"Paciente ID {p.Id}"
                    };
                })
                .OrderBy(p => p.Nome)
                .ToList();

            pck_paciente.ItemsSource = null;
            pck_paciente.ItemDisplayBinding = new Binding(nameof(PacienteExibicao.Nome));
            pck_paciente.ItemsSource = lista;

            if (idSelecionado.HasValue)
                pck_paciente.SelectedItem = lista.FirstOrDefault(p => p.Paciente.Id == idSelecionado.Value);

            if (lista.Count == 0)
                MostrarMensagem("Nenhum paciente cadastrado. Cadastre um paciente e registre o histórico vacinal.");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível carregar os pacientes.\n\n{ex.Message}", "OK");
        }
        finally
        {
            carregandoPacientes = false;
        }

        if (pck_paciente.SelectedItem is PacienteExibicao)
            await GerarAnalise();
    }

    private async void pck_paciente_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (carregandoPacientes) return;
        await GerarAnalise();
    }

    // =====================================================
    // GERAR ANÁLISE
    // =====================================================

    private async Task GerarAnalise()
    {
        if (pck_paciente.SelectedItem is not PacienteExibicao selecionado)
        {
            resultadoAtual = null;
            return;
        }

        selecionadoAtual = selecionado;
        DefinirCarregando(true);

        try
        {
            var servico = new ServicoAnalise(App.Database);
            var resultado = await servico.AnalisarAsync(selecionado.Paciente, pessoa: selecionado.Pessoa);
            resultadoAtual = resultado;

            MostrarResultado(resultado);

            if (resultado.Situacao != SituacaoGeral.NaoAnalisado)
                await servico.SalvarAsync(resultado);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro na análise", $"Não foi possível gerar a análise.\n\n{ex.Message}", "OK");
        }
        finally
        {
            DefinirCarregando(false);
        }
    }

    private void DefinirCarregando(bool carregando)
    {
        ind_carregando.IsVisible = carregando;
        ind_carregando.IsRunning = carregando;
    }

    // =====================================================
    // MONTAGEM DOS CARTÕES
    // =====================================================

    private void MostrarMensagem(string texto)
    {
        pnl_resultado.Children.Clear();
        pnl_resultado.Children.Add(new Label
        {
            Text = texto,
            TextColor = CorTexto,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        });
    }

    private void MostrarResultado(ResultadoAnalise r)
    {
        pnl_resultado.Children.Clear();

        // ---------- cabeçalho com a situação geral ----------
        Color corSituacao = r.Situacao switch
        {
            SituacaoGeral.Regular => CorEmDia,
            SituacaoGeral.ComDosesParaAplicar => CorHoje,
            SituacaoGeral.EmAtraso => CorAtrasada,
            _ => CorFora
        };

        var cabecalho = new VerticalStackLayout { Spacing = 4 };
        cabecalho.Children.Add(Texto(r.NomePaciente, 18, true, CorTitulo));
        cabecalho.Children.Add(Texto($"{r.IdadeTexto} · {r.GrupoEtario}", 14));
        if (r.DataNascimento != default)
            cabecalho.Children.Add(Texto($"Nascimento: {r.DataNascimento:dd/MM/yyyy}", 13));
        if (r.CondicoesConsideradas.Count > 0)
            cabecalho.Children.Add(Texto("Condições: " + string.Join(", ", r.CondicoesConsideradas), 13));

        cabecalho.Children.Add(Etiqueta(r.SituacaoTexto + (r.RequerAvaliacao ? " · REQUER AVALIAÇÃO" : ""), corSituacao, 15));
        cabecalho.Children.Add(Texto(
            $"Atrasadas: {r.Atrasadas.Count()}   ·   Aplicar hoje: {r.ParaAplicarHoje.Count()}   ·   " +
            $"Próximas: {r.Proximas.Count()}   ·   Em dia: {r.EmDia.Count()}", 13, true));
        pnl_resultado.Children.Add(Cartao(cabecalho, corSituacao));

        // ---------- avisos e alertas ----------
        foreach (var aviso in r.Avisos)
            pnl_resultado.Children.Add(Cartao(Texto("⚠ " + aviso, 13), CorHoje));

        foreach (var alerta in r.Alertas)
        {
            Color cor = alerta.Nivel switch
            {
                NivelAlerta.Bloqueio => CorAtrasada,
                NivelAlerta.Atencao => CorHoje,
                _ => CorTitulo
            };
            pnl_resultado.Children.Add(Cartao(Texto(alerta.ToString(), 13), cor));
        }

        if (r.Situacao == SituacaoGeral.NaoAnalisado)
            return;

        // ---------- seções ----------
        Secao("Vacinas em atraso", r.Atrasadas, permitirRegistro: true);
        Secao("Vacinas para aplicar hoje", r.ParaAplicarHoje, permitirRegistro: true);
        Secao("Contraindicações e precauções", r.ComRestricao.Where(i => !i.EhRedePrivada), mostrarRestricao: true);
        MostrarPlano(r);
        Secao("Próximas doses", r.Proximas);
        Secao("Esquemas em dia", r.EmDia, compacto: true);
        Secao("Fora da faixa etária (não recuperar)", r.ForaDaFaixa, compacto: true);
        Secao("Rede privada (complementares)", r.ItensRedePrivada, compacto: true);
        MostrarHistorico(r);

        pnl_resultado.Children.Add(Texto(
            "A análise é um apoio à decisão e não substitui a avaliação do profissional de saúde. " +
            "Condições especiais podem exigir avaliação no CRIE.", 12));
    }

    private void Secao(string titulo, IEnumerable<ItemAnalise> itens, bool permitirRegistro = false,
                       bool mostrarRestricao = false, bool compacto = false)
    {
        var lista = itens.ToList();
        if (lista.Count == 0) return;

        pnl_resultado.Children.Add(Texto($"{titulo} ({lista.Count})", 17, true, CorTitulo));
        foreach (var item in lista)
            pnl_resultado.Children.Add(CartaoItem(item, permitirRegistro, mostrarRestricao, compacto));
    }

    private View CartaoItem(ItemAnalise i, bool permitirRegistro, bool mostrarRestricao, bool compacto)
    {
        Color cor = CorDoItem(i);
        var corpo = new VerticalStackLayout { Spacing = 4 };

        var linhaTitulo = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        linhaTitulo.Add(Texto(i.Vacina, 16, true, Colors.Black), 0, 0);
        linhaTitulo.Add(Etiqueta(i.Etiqueta, cor, 11), 1, 0);
        corpo.Children.Add(linhaTitulo);

        corpo.Children.Add(Texto(i.DescricaoDoses + (string.IsNullOrWhiteSpace(i.Via) ? "" : $" · via {i.Via}"), 13, true));
        corpo.Children.Add(Texto(mostrarRestricao ? i.MotivoRestricao : i.Motivo, 13));

        if (!compacto)
        {
            if (!mostrarRestricao && !string.IsNullOrWhiteSpace(i.MotivoRestricao))
                corpo.Children.Add(Texto("⚠ " + i.MotivoRestricao, 13, false, CorRestricao));
            if (!string.IsNullOrWhiteSpace(i.Orientacao))
                corpo.Children.Add(Texto("Conduta sugerida: " + i.Orientacao, 13));
            if (i.DosesRestantes.Count > 1)
                corpo.Children.Add(Texto("Doses previstas: " + string.Join(" · ",
                    i.DosesRestantes.Select(d => $"{d.NumeroDose}ª em {d.DataPrevista:dd/MM/yyyy}")), 12));
            if (!string.IsNullOrWhiteSpace(i.Observacao))
                corpo.Children.Add(Texto("Obs.: " + i.Observacao, 12));
        }

        if (permitirRegistro && i.IndicadaHoje && !i.EhRedePrivada)
        {
            var botao = new Button
            {
                Text = $"Registrar {i.RotuloProximaDose} aplicada hoje",
                BackgroundColor = CorEmDia,
                TextColor = Colors.White,
                FontSize = 13,
                Margin = new Thickness(0, 6, 0, 0)
            };
            botao.Clicked += async (_, _) => await RegistrarDoseAsync(i);
            corpo.Children.Add(botao);
        }

        return Cartao(corpo, cor);
    }

    private void MostrarPlano(ResultadoAnalise r)
    {
        if (r.PlanoRecuperacao.Count == 0) return;

        pnl_resultado.Children.Add(Texto("Esquema de recuperação — próximos atendimentos", 17, true, CorTitulo));

        foreach (var a in r.PlanoRecuperacao)
        {
            var corpo = new VerticalStackLayout { Spacing = 3 };
            corpo.Children.Add(Texto(
                $"Atendimento {a.Numero} — {(a.EhHoje ? "hoje, " : "a partir de ")}{a.Data:dd/MM/yyyy}", 15, true, CorTitulo));
            foreach (var d in a.Doses)
                corpo.Children.Add(Texto("• " + d, 13, false, d.Atrasada ? CorAtrasada : CorTexto));
            foreach (var o in a.Observacoes)
                corpo.Children.Add(Texto("obs.: " + o, 12));
            pnl_resultado.Children.Add(Cartao(corpo, a.EhHoje ? CorHoje : CorProxima));
        }
    }

    private void MostrarHistorico(ResultadoAnalise r)
    {
        pnl_resultado.Children.Add(Texto("Histórico registrado", 17, true, CorTitulo));
        var corpo = new VerticalStackLayout { Spacing = 2 };
        if (r.Historico.Count == 0)
            corpo.Children.Add(Texto("Nenhuma dose registrada. Use a tela Histórico Vacinal para lançar a caderneta.", 13));
        foreach (var h in r.Historico)
            corpo.Children.Add(Texto($"{h.Vacina}: {h.Doses} dose(s)" +
                (h.UltimaAplicacao.HasValue ? $" — última em {h.UltimaAplicacao:dd/MM/yyyy}" : ""), 13));
        pnl_resultado.Children.Add(Cartao(corpo, CorFora));
    }

    private static Color CorDoItem(ItemAnalise i)
    {
        if (i.Restricao != Restricao.Nenhuma) return CorRestricao;
        return i.Prazo switch
        {
            SituacaoPrazo.Atrasada => CorAtrasada,
            SituacaoPrazo.AplicarHoje => CorHoje,
            SituacaoPrazo.EmDia => CorEmDia,
            SituacaoPrazo.ForaDaFaixaEtaria => CorFora,
            _ => CorProxima
        };
    }

    private static Label Texto(string texto, double tamanho, bool negrito = false, Color? cor = null) => new()
    {
        Text = texto,
        FontSize = tamanho,
        FontAttributes = negrito ? FontAttributes.Bold : FontAttributes.None,
        TextColor = cor ?? CorTexto,
        LineBreakMode = LineBreakMode.WordWrap
    };

    private static View Etiqueta(string texto, Color cor, double tamanho) => new Border
    {
        BackgroundColor = cor,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 10 },
        Padding = new Thickness(8, 2),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Center,
        Content = new Label { Text = texto, TextColor = Colors.White, FontSize = tamanho, FontAttributes = FontAttributes.Bold }
    };

    private static View Cartao(View conteudo, Color corBorda)
    {
        var grade = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(5)), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 10
        };
        grade.Add(new BoxView { Color = corBorda, CornerRadius = 2 }, 0, 0);
        grade.Add(conteudo, 1, 0);

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#D0D5DD"),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(10),
            Content = grade
        };
    }

    // =====================================================
    // REGISTRAR DOSE APLICADA (RF03) DIRETO DA ANÁLISE
    // =====================================================

    private async Task RegistrarDoseAsync(ItemAnalise item)
    {
        if (selecionadoAtual == null) return;

        bool confirmar = await DisplayAlert(
            "Registrar vacinação",
            $"Registrar {item.Vacina} — {item.RotuloProximaDose}, aplicada hoje ({DateTime.Today:dd/MM/yyyy}), " +
            $"para {selecionadoAtual.Nome}?",
            "Registrar", "Cancelar");
        if (!confirmar) return;

        string? lote = await DisplayPromptAsync("Lote", "Informe o lote da vacina (opcional):", "OK", "Pular",
            maxLength: 40, keyboard: Keyboard.Text);

        try
        {
            var registro = new HistoricoVacinal
            {
                PacienteId = selecionadoAtual.Paciente.Id,
                NomePaciente = selecionadoAtual.Nome,
                RegraVacinalId = item.RegraProximaDoseId ?? 0,
                NomeVacina = item.Vacina,
                DosesTomadas = 1,
                DataAplicacao = DateTime.Today,
                DataUltimaAplicacao = DateTime.Today,
                DataRegistro = DateTime.Now,
                LocalAplicacao = string.Empty,
                Lote = lote?.Trim() ?? string.Empty,
                ProfissionalResponsavel = string.Empty,
                Observacao = $"Registrada pela análise vacinal ({item.RotuloProximaDose})."
            };

            await App.Database.SalvarHistoricoVacinal(registro);
            await GerarAnalise();
            await DisplayAlert("Dose registrada", $"{item.Vacina} registrada no histórico. A análise foi atualizada.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível registrar a dose.\n\n{ex.Message}", "OK");
        }
    }

    // =====================================================
    // COMPARTILHAR (texto) E RELATÓRIO (HTML para imprimir/PDF)
    // =====================================================

    private async void Button_Compartilhar_Clicked(object sender, EventArgs e)
    {
        if (resultadoAtual == null)
        {
            await DisplayAlert("Atenção", "Selecione um paciente e gere a análise primeiro.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Análise Vacinal Amar V",
            Text = FormatadorTexto.Gerar(resultadoAtual)
        });
    }

    private async void Button_Imprimir_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (resultadoAtual == null)
            {
                await DisplayAlert("Atenção", "Selecione um paciente e gere a análise primeiro.", "OK");
                return;
            }

            // Pasta interna do app: funciona no Android, iOS e Windows.
            string pasta = System.IO.Path.Combine(FileSystem.CacheDirectory, "relatorios");
            Directory.CreateDirectory(pasta);
            string nome = $"analise_vacinal_{resultadoAtual.PacienteId}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string caminho = System.IO.Path.Combine(pasta, nome);

            File.WriteAllText(caminho, FormatadorHtml.Gerar(resultadoAtual), Encoding.UTF8);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Relatório vacinal Amar V",
                File = new ShareFile(caminho, "text/html")
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro ao gerar relatório", ex.Message, "OK");
        }
    }

    private async void Button_Menu_Clicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
