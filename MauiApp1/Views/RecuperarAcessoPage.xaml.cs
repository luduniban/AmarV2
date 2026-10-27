
using System;
using System.Linq;
using Microsoft.Maui.Controls;
using maui_amarv.Models;

namespace maui_amarv.Views;

public partial class RecuperarAcessoPage : ContentPage
{
    public RecuperarAcessoPage()
    {
        InitializeComponent();

        date_nascimento.MaximumDate = DateTime.Today;
        date_nascimento.Date = DateTime.Today;
    }

    private async void Button_Redefinir_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            string cpf = entry_cpf.Text?.Trim() ?? string.Empty;
            string novaSenha = entry_nova_senha.Text ?? string.Empty;
            string confirmacao =
                entry_confirmar_senha.Text ?? string.Empty;

            string cpfLimpo = new string(
                cpf.Where(char.IsDigit).ToArray());

            if (cpfLimpo.Length != 11)
            {
                await DisplayAlert(
                    "Atenção",
                    "Informe um CPF com 11 números.",
                    "OK");
                return;
            }

            if (date_nascimento.Date > DateTime.Today)
            {
                await DisplayAlert(
                    "Atenção",
                    "A data de nascimento não pode ser futura.",
                    "OK");
                return;
            }

            if (novaSenha.Length < 6)
            {
                await DisplayAlert(
                    "Atenção",
                    "A senha deve ter pelo menos 6 caracteres.",
                    "OK");
                return;
            }

            if (novaSenha != confirmacao)
            {
                await DisplayAlert(
                    "Atenção",
                    "As senhas informadas são diferentes.",
                    "OK");
                return;
            }

            await App.Database.InicializarAsync();

            DateTime? dataSelecionada = date_nascimento.Date;

            if (!dataSelecionada.HasValue)
            {
                await DisplayAlert(
                    "Atenção",
                    "Informe a data de nascimento.",
                    "OK");
                return;
            }

            UsuarioDoSistema? usuario =
                await App.Database.RecuperarAcessoAsync(
                    cpfLimpo,
                    dataSelecionada.Value,
                    novaSenha);

            if (usuario == null)
            {
                await DisplayAlert(
                    "Recuperação não concluída",
                    "Não foi possível confirmar os dados ou localizar " +
                    "uma conta ativa vinculada a esse cadastro. " +
                    "Confira o CPF e a data de nascimento.",
                    "OK");
                return;
            }

            entry_nova_senha.Text = string.Empty;
            entry_confirmar_senha.Text = string.Empty;

            await DisplayAlert(
                "Senha redefinida",
                $"A senha foi alterada com sucesso.\n\n" +
                $"Login: {usuario.Login}\n\n" +
                "Volte à tela de login para acessar o sistema.",
                "OK");

            await Navigation.PopAsync();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert(
                "Atenção",
                ex.Message,
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Erro",
                $"Não foi possível redefinir a senha: {ex.Message}",
                "OK");
        }
    }

    private async void Button_Voltar_Clicked(
        object sender,
        EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
