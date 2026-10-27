
using Microsoft.Maui.Controls;
using maui_amarv.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace maui_amarv.Views
{
    public partial class CadastroPaciente : ContentPage
    {
        private Pessoa? pessoaSelecionada;
        private List<Pessoa> pessoas = new();
        private Paciente? pacienteSelecionado;
        private bool atualizandoCondicoes;
        private bool modoEdicao;
        private bool nascimentoInformado;

        public CadastroPaciente()
        {
            InitializeComponent();

            dp_nascimento.MaximumDate = DateTime.Today;
            dp_nascimento.Date = DateTime.Today;
        }

        private void Dp_Nascimento_DateSelected(
            object? sender,
            DateChangedEventArgs e)
        {
            nascimentoInformado = true;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CarregarPessoas();
        }

        // =====================================================
        // CARREGAR PESSOAS
        // =====================================================

        private async Task CarregarPessoas()
        {
            try
            {
                // Contas do sistema (admin e usuários) também são "Pessoa",
                // mas não são pacientes: ficam fora da lista.
                var idsUsuarios = (await App.Database.Listar<UsuarioDoSistema>())
                    .Select(u => u.Id)
                    .ToHashSet();

                pessoas = (await App.Database.Listar<Pessoa>())
                    .Where(p => !idsUsuarios.Contains(p.Id))
                    .ToList();

                pck_pessoa.ItemsSource = null;
                pck_pessoa.ItemsSource = pessoas
                    .OrderBy(p => p.Nome)
                    .ToList();

                pck_pessoa.ItemDisplayBinding =
                    new Binding(nameof(Pessoa.Nome));

                if (pessoaSelecionada != null)
                {
                    pessoaSelecionada = pessoas.FirstOrDefault(
                        p => p.Id == pessoaSelecionada.Id);

                    pck_pessoa.SelectedItem = pessoaSelecionada;
                }

                if (pessoaSelecionada == null)
                    DefinirModoPessoa(novaPessoa: true);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro",
                    $"Não foi possível carregar as pessoas.\n{ex.Message}",
                    "OK");
            }
        }

        // =====================================================
        // SELECIONAR PESSOA
        // =====================================================

        private async void Pck_Pessoa_SelectedIndexChanged(
            object sender, EventArgs e)
        {
            pessoaSelecionada = pck_pessoa.SelectedItem as Pessoa;

            if (pessoaSelecionada == null)
            {
                LimparCamposPessoa();
                DefinirModoPessoa(novaPessoa: true);
                return;
            }

            entry_nome.Text = pessoaSelecionada.Nome ?? string.Empty;
            entry_cpf.Text = pessoaSelecionada.Cpf ?? string.Empty;
            entry_nome_mae.Text =
                pessoaSelecionada.Nome_da_Mae ?? string.Empty;
            entry_telefone.Text =
                pessoaSelecionada.Telefone ?? string.Empty;
            entry_endereco.Text =
                pessoaSelecionada.Endereco ?? string.Empty;

            DefinirModoPessoa(novaPessoa: false);

            // Ao selecionar outra pessoa, sai do modo de edição.
            pacienteSelecionado = null;
            modoEdicao = false;

            try
            {
                var paciente = await App.Database.BuscarPaciente(
                    pessoaSelecionada.Id);

                if (paciente != null)
                {
                    await CarregarDadosPaciente(paciente);
                }
                else
                {
                    await LimparCamposPaciente();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro",
                    $"Não foi possível consultar o paciente.\n{ex.Message}",
                    "OK");
            }
        }

        // Novo paciente: campos pessoais liberados para digitação.
        // Pessoa escolhida na lista: campos só para conferência.
        private void DefinirModoPessoa(bool novaPessoa)
        {
            entry_nome.IsReadOnly = !novaPessoa;
            entry_cpf.IsReadOnly = !novaPessoa;
            entry_nome_mae.IsReadOnly = !novaPessoa;
            entry_telefone.IsReadOnly = !novaPessoa;
            entry_endereco.IsReadOnly = !novaPessoa;

            lbl_modo_pessoa.Text = novaPessoa
                ? "Novo paciente: preencha os dados pessoais abaixo."
                : "Pessoa já cadastrada selecionada (dados pessoais só para conferência).";
        }

        private async void Button_NovoPaciente_Clicked(object sender, EventArgs e)
        {
            pessoaSelecionada = null;
            pacienteSelecionado = null;
            modoEdicao = false;
            pck_pessoa.SelectedItem = null;
            LimparCamposPessoa();
            DefinirModoPessoa(novaPessoa: true);
            await LimparCamposPaciente();
            entry_nome.Focus();
        }

        private void LimparCamposPessoa()
        {
            entry_nome.Text = string.Empty;
            entry_cpf.Text = string.Empty;
            entry_nome_mae.Text = string.Empty;
            entry_telefone.Text = string.Empty;
            entry_endereco.Text = string.Empty;
        }

        // =====================================================
        // CARREGAR DADOS DO PACIENTE PARA EDIÇÃO
        // =====================================================

        private async Task CarregarDadosPaciente(Paciente paciente)
        {
            pacienteSelecionado = paciente;

            pck_sexo.SelectedItem = paciente.Sexo;
            entry_cartao_sus.Text = paciente.CartaoSUS;
            dp_nascimento.Date =
                paciente.DataNascimento == default
                    ? DateTime.Today
                    : paciente.DataNascimento.Date;
            nascimentoInformado = paciente.DataNascimento != default;

            chk_gestante.IsChecked = paciente.Gestante;
            entry_semanas_gestacao.Text =
                paciente.SemanasGestacao > 0
                    ? paciente.SemanasGestacao.ToString()
                    : string.Empty;

            chk_lactante.IsChecked = paciente.Lactante;
            chk_puerpera.IsChecked = paciente.Puerpera;
            chk_lactante_bebe.IsChecked =
                paciente.CriancaAmamentadaMenor6Meses;
            chk_profissional_saude.IsChecked =
                paciente.ProfissionalSaude;
            chk_indigena.IsChecked = paciente.Indigena;
            chk_acamado.IsChecked =
                paciente.AcamadoOuInstitucionalizado;
            chk_imunossuprimido.IsChecked =
                paciente.Imunossuprimido;
            chk_hiv.IsChecked = paciente.HIV;
            chk_quimioterapia.IsChecked = paciente.Quimioterapia;
            chk_transplante.IsChecked = paciente.Transplantado;
            chk_corticoide.IsChecked =
                paciente.UsoCorticoideOuImunobiologico;
            chk_alergia.IsChecked = paciente.AlergiaGrave;
            chk_febre.IsChecked = paciente.FebreOuDoencaAguda;

            chk_nenhuma.IsChecked =
                paciente.CondicaoClinica == "Nenhuma";

            entry_semanas_gestacao.IsEnabled = paciente.Gestante;

            await Task.CompletedTask;
        }

        // =====================================================
        // BOTÃO EDITAR
        // =====================================================

        private async void Button_EditarPaciente_Clicked(
            object sender, EventArgs e)
        {
            if (pessoaSelecionada == null)
            {
                await DisplayAlert(
                    "Atenção",
                    "Selecione uma pessoa cadastrada.",
                    "OK");
                return;
            }

            try
            {
                var paciente = await App.Database.BuscarPaciente(
                    pessoaSelecionada.Id);

                if (paciente == null)
                {
                    await DisplayAlert(
                        "Atenção",
                        "Esta pessoa ainda não possui cadastro de paciente. " +
                        "Utilize o botão de cadastro.",
                        "OK");
                    return;
                }

                await CarregarDadosPaciente(paciente);
                modoEdicao = true;

                await DisplayAlert(
                    "Editar paciente",
                    "Altere os dados e clique em Salvar.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro",
                    $"Não foi possível iniciar a edição.\n{ex.Message}",
                    "OK");
            }
        }

        // =====================================================
        // CONDIÇÕES CLÍNICAS
        // =====================================================

        private void Condicao_CheckedChanged(
            object sender, CheckedChangedEventArgs e)
        {
            if (atualizandoCondicoes)
                return;

            atualizandoCondicoes = true;

            try
            {
                if (sender == chk_nenhuma && e.Value)
                {
                    chk_gestante.IsChecked = false;
                    chk_lactante.IsChecked = false;
                    chk_lactante_bebe.IsChecked = false;
                    chk_puerpera.IsChecked = false;
                    chk_profissional_saude.IsChecked = false;
                    chk_indigena.IsChecked = false;
                    chk_acamado.IsChecked = false;
                    chk_imunossuprimido.IsChecked = false;
                    chk_hiv.IsChecked = false;
                    chk_quimioterapia.IsChecked = false;
                    chk_transplante.IsChecked = false;
                    chk_corticoide.IsChecked = false;
                    chk_alergia.IsChecked = false;
                    chk_febre.IsChecked = false;

                    entry_semanas_gestacao.Text = string.Empty;
                }
                else if (sender != chk_nenhuma && e.Value)
                {
                    chk_nenhuma.IsChecked = false;
                }

                // Bebê com até 6 meses implica lactante.
                if (sender == chk_lactante_bebe && e.Value)
                    chk_lactante.IsChecked = true;

                if (sender == chk_lactante && !e.Value)
                    chk_lactante_bebe.IsChecked = false;

                // Gestante e puérpera são situações excludentes.
                if (sender == chk_gestante && e.Value)
                    chk_puerpera.IsChecked = false;

                if (sender == chk_puerpera && e.Value)
                    chk_gestante.IsChecked = false;

                bool gestante = chk_gestante.IsChecked;
                entry_semanas_gestacao.IsEnabled = gestante;

                if (!gestante)
                    entry_semanas_gestacao.Text = string.Empty;
            }
            finally
            {
                atualizandoCondicoes = false;
            }
        }

        // =====================================================
        // CADASTRAR OU EDITAR PACIENTE
        // =====================================================

        private async void Button_Clicked(
            object sender, EventArgs e)
        {
            try
            {
                // Paciente novo: valida os dados digitados. A Pessoa só é
                // gravada depois de validar também os dados do paciente.
                bool criarPessoa = pessoaSelecionada == null;
                Pessoa? novaPessoa = null;
                if (criarPessoa)
                {
                    string cpfDigitado = new string(
                        (entry_cpf.Text ?? string.Empty).Where(char.IsDigit).ToArray());

                    if (string.IsNullOrWhiteSpace(entry_nome.Text))
                    {
                        await DisplayAlert("Atenção", "Informe o nome completo do paciente.", "OK");
                        return;
                    }

                    if (cpfDigitado.Length != 11)
                    {
                        await DisplayAlert("Atenção", "Informe o CPF com 11 números.", "OK");
                        return;
                    }

                    novaPessoa = new Pessoa
                    {
                        Nome = entry_nome.Text.Trim(),
                        Cpf = cpfDigitado,
                        Nome_da_Mae = entry_nome_mae.Text?.Trim() ?? string.Empty,
                        Telefone = entry_telefone.Text?.Trim() ?? string.Empty,
                        Endereco = entry_endereco.Text?.Trim() ?? string.Empty
                    };
                }

                // Pessoa usada neste salvamento (a escolhida ou a nova).
                // pessoaSelecionada só muda depois que a Pessoa é gravada.
                Pessoa pessoaAtual = criarPessoa ? novaPessoa! : pessoaSelecionada!;

                string cpf = new string(
                    (pessoaAtual.Cpf ?? string.Empty)
                    .Where(char.IsDigit)
                    .ToArray());

                if (string.IsNullOrWhiteSpace(pessoaAtual.Nome))
                {
                    await DisplayAlert(
                        "Atenção",
                        "O nome da pessoa é obrigatório.",
                        "OK");
                    return;
                }

                if (cpf.Length != 11)
                {
                    await DisplayAlert(
                        "Atenção",
                        "O CPF deve conter 11 dígitos.",
                        "OK");
                    return;
                }

                if (pck_sexo.SelectedItem == null)
                {
                    await DisplayAlert(
                        "Atenção",
                        "Selecione o sexo do paciente.",
                        "OK");
                    return;
                }

                // Data de nascimento: a idade passa a ser sempre calculada
                // a partir dela. Se nada foi escolhido (data = hoje),
                // confirma para evitar cadastrar um recém-nascido por engano.
                if (dp_nascimento.Date == null)
                {
                    await DisplayAlert(
                        "Atenção",
                        "Informe a data de nascimento.",
                        "OK");
                    return;
                }

                if (!nascimentoInformado &&
                    dp_nascimento.Date == DateTime.Today)
                {
                    bool nasceuHoje = await DisplayAlert(
                        "Data de nascimento",
                        "A data de nascimento está como hoje. " +
                        "Confirma que o paciente nasceu hoje?",
                        "Sim, nasceu hoje",
                        "Corrigir data");

                    if (!nasceuHoje)
                        return;
                }

                int semanasGestacao = 0;

                if (chk_gestante.IsChecked
                    && (!int.TryParse(
                            entry_semanas_gestacao.Text,
                            out semanasGestacao)
                        || semanasGestacao < 1
                        || semanasGestacao > 45))
                {
                    await DisplayAlert(
                        "Atenção",
                        "Informe as semanas de gestação, de 1 a 45.",
                        "OK");
                    return;
                }

                if (criarPessoa)
                {
                    // Grava a Pessoa (mesma data de nascimento do paciente) e
                    // usa o Id gerado como Id do paciente.
                    pessoaAtual.DataNascimento = dp_nascimento.Date!.Value.Date;
                    await App.Database.SalvarPessoa(pessoaAtual);
                    pessoaSelecionada = pessoaAtual;
                    DefinirModoPessoa(novaPessoa: false);
                }

                var condicoes = new List<string>();

                if (chk_gestante.IsChecked) condicoes.Add("Gestante");
                if (chk_lactante.IsChecked) condicoes.Add("Lactante");
                if (chk_puerpera.IsChecked) condicoes.Add("Puérpera");
                if (chk_lactante_bebe.IsChecked)
                    condicoes.Add("Lactante com bebê menor de 6 meses");
                if (chk_profissional_saude.IsChecked)
                    condicoes.Add("Profissional de saúde");
                if (chk_indigena.IsChecked) condicoes.Add("Pessoa indígena");
                if (chk_acamado.IsChecked)
                    condicoes.Add("Acamado/institucionalizado");
                if (chk_imunossuprimido.IsChecked)
                    condicoes.Add("Imunossuprimido/imunodeficiência");
                if (chk_hiv.IsChecked) condicoes.Add("HIV");
                if (chk_quimioterapia.IsChecked)
                    condicoes.Add("Quimioterapia");
                if (chk_transplante.IsChecked)
                    condicoes.Add("Transplantado");
                if (chk_corticoide.IsChecked)
                    condicoes.Add("Uso de corticoide ou imunobiológico");
                if (chk_alergia.IsChecked)
                    condicoes.Add("Alergia grave/anafilaxia");
                if (chk_febre.IsChecked)
                    condicoes.Add("Febre, infecção ou doença aguda");
                if (chk_nenhuma.IsChecked)
                    condicoes.Add("Nenhuma");

                var paciente = new Paciente
                {
                    Id = pessoaAtual.Id,
                    DataNascimento = dp_nascimento.Date!.Value.Date,
                    Sexo = pck_sexo.SelectedItem.ToString()
                        ?? string.Empty,
                    CartaoSUS =
                        entry_cartao_sus.Text?.Trim() ?? string.Empty,
                    CondicaoClinica = string.Join("; ", condicoes),
                    Gestante = chk_gestante.IsChecked,
                    SemanasGestacao = semanasGestacao,
                    Lactante = chk_lactante.IsChecked,
                    Puerpera = chk_puerpera.IsChecked,
                    CriancaAmamentadaMenor6Meses =
                        chk_lactante_bebe.IsChecked,
                    ProfissionalSaude =
                        chk_profissional_saude.IsChecked,
                    Indigena = chk_indigena.IsChecked,
                    AcamadoOuInstitucionalizado =
                        chk_acamado.IsChecked,
                    Imunossuprimido = chk_imunossuprimido.IsChecked,
                    HIV = chk_hiv.IsChecked,
                    Quimioterapia = chk_quimioterapia.IsChecked,
                    Transplantado = chk_transplante.IsChecked,
                    UsoCorticoideOuImunobiologico =
                        chk_corticoide.IsChecked,
                    AlergiaGrave = chk_alergia.IsChecked,
                    FebreOuDoencaAguda = chk_febre.IsChecked
                };

                int idPaciente;

                if (modoEdicao && pacienteSelecionado != null)
                {
                    paciente.Id = pacienteSelecionado.Id;

                    idPaciente = await App.Database.EditarPaciente(
                        paciente, pessoaAtual);
                }
                else
                {
                    // Não cria duplicidade para uma pessoa que já
                    // possui registro na tabela Paciente.
                    var existente = await App.Database.BuscarPaciente(
                        pessoaAtual.Id);

                    if (existente != null)
                    {
                        await DisplayAlert(
                            "Atenção",
                            "Esta pessoa já possui cadastro de paciente. " +
                            "Clique em Editar Paciente para alterar os dados.",
                            "OK");
                        return;
                    }

                    idPaciente = await App.Database.SalvarPaciente(
                        paciente, pessoaAtual);
                }

                if (idPaciente <= 0)
                {
                    await DisplayAlert(
                        "Erro",
                        "Não foi possível salvar o paciente.",
                        "OK");
                    return;
                }

                modoEdicao = false;
                pacienteSelecionado = null;

                await DisplayAlert(
                    "Sucesso",
                    "Cadastro do paciente salvo com sucesso.",
                    "OK");

                await Navigation.PushAsync(
                    new HistoricoVacinalPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro ao salvar",
                    ex.Message,
                    "OK");
            }
        }

        // =====================================================
        // EXCLUIR PACIENTE
        // =====================================================

        private async void Button_ExcluirPaciente_Clicked(
            object sender, EventArgs e)
        {
            try
            {
                if (pessoaSelecionada == null)
                {
                    await DisplayAlert(
                        "Atenção",
                        "Selecione uma pessoa cadastrada.",
                        "OK");
                    return;
                }

                var paciente = await App.Database.BuscarPaciente(
                    pessoaSelecionada.Id);

                if (paciente == null)
                {
                    await DisplayAlert(
                        "Atenção",
                        "Esta pessoa não possui cadastro de paciente.",
                        "OK");
                    return;
                }

                bool confirmar = await DisplayAlert(
                    "Confirmar exclusão",
                    $"Deseja excluir o cadastro de paciente de " +
                    $"{pessoaSelecionada.Nome}? " +
                    "Os históricos vacinais relacionados também serão excluídos. " +
                    "Os dados pessoais serão preservados.",
                    "Excluir",
                    "Cancelar");

                if (!confirmar)
                    return;

                await App.Database.ExcluirPacienteComHistorico(
                    paciente.Id);

                pacienteSelecionado = null;
                modoEdicao = false;

                await LimparCamposPaciente();

                await DisplayAlert(
                    "Sucesso",
                    "Cadastro de paciente excluído. " +
                    "Os dados pessoais foram preservados.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro ao excluir",
                    ex.Message,
                    "OK");
            }
        }

        // =====================================================
        // LIMPAR CAMPOS DO PACIENTE
        // =====================================================

        private async Task LimparCamposPaciente()
        {
            pck_sexo.SelectedItem = null;
            entry_cartao_sus.Text = string.Empty;
            dp_nascimento.Date = DateTime.Today;
            nascimentoInformado = false;
            entry_semanas_gestacao.Text = string.Empty;

            atualizandoCondicoes = true;

            try
            {
                chk_nenhuma.IsChecked = false;
                chk_gestante.IsChecked = false;
                chk_lactante.IsChecked = false;
                chk_lactante_bebe.IsChecked = false;
                chk_puerpera.IsChecked = false;
                chk_profissional_saude.IsChecked = false;
                chk_indigena.IsChecked = false;
                chk_acamado.IsChecked = false;
                chk_imunossuprimido.IsChecked = false;
                chk_hiv.IsChecked = false;
                chk_quimioterapia.IsChecked = false;
                chk_transplante.IsChecked = false;
                chk_corticoide.IsChecked = false;
                chk_alergia.IsChecked = false;
                chk_febre.IsChecked = false;
            }
            finally
            {
                atualizandoCondicoes = false;
            }

            entry_semanas_gestacao.IsEnabled = false;
            await Task.CompletedTask;
        }

        // =====================================================
        // VOLTAR AO MENU PRINCIPAL
        // =====================================================

        // =====================================================
        // VOLTAR AO MENU PRINCIPAL
        // =====================================================


        private async void Button_Menu_Clicked(
            object sender, EventArgs e)
        {
            try
            {
                if (Navigation.NavigationStack.Count > 1)
                {
                    // Retorna ao MenuPrincipal já aberto,
                    // preservando a sessão do usuário.
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert(
                        "Navegação",
                        "Não foi possível localizar o menu anterior. " +
                        "Entre novamente no sistema.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Erro",
                    $"Não foi possível voltar ao menu.\n{ex.Message}",
                    "OK");
            }
        }
    }
}