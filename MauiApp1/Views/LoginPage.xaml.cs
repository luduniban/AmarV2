using System;
using Microsoft.Maui.Controls;
using maui_amarv.Models;

namespace maui_amarv.Views;

public partial class LoginPage : ContentPage
{
    private bool _processandoLogin;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void Button_Entrar_Clicked(
        object sender,
        EventArgs e)
    {
        if (_processandoLogin)
            return;

        _processandoLogin = true;

        try
        {
            // 1. Validar o login.
            string login = entry_login.Text?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(login))
            {
                await DisplayAlert(
                    "Atenção",
                    "Digite seu login.",
                    "OK");

                entry_login.Focus();
                return;
            }

            // 2. Validar a senha.
            string senha = entry_senha.Text
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(senha))
            {
                await DisplayAlert(
                    "Atenção",
                    "Digite sua senha.",
                    "OK");

                entry_senha.Focus();
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                "LOGIN: iniciando autenticação");

            // 3. Garantir que o banco esteja inicializado.
            await App.Database.InicializarAsync();

            // 4. Buscar a conta e validar a senha
            // por meio de UsuarioHelper e SenhaHelper.
            UsuarioDoSistema? usuario =
                await App.Database.BuscarUsuario(
                    login,
                    senha);

            System.Diagnostics.Debug.WriteLine(
                $"LOGIN: usuário autenticado = {usuario != null}");

            if (usuario == null)
            {
                await DisplayAlert(
                    "Acesso negado",
                    "Login ou senha inválidos, ou conta inativa.",
                    "OK");

                entry_senha.Text = string.Empty;
                entry_senha.Focus();
                return;
            }

            // 5. Confirmar o status da conta.
            if (!usuario.Ativo ||
                !string.Equals(
                    usuario.Status?.Trim(),
                    "Ativo",
                    StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert(
                    "Acesso bloqueado",
                    "Sua conta não está ativa. " +
                    "Entre em contato com o administrador do sistema.",
                    "OK");

                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"LOGIN: perfil = {usuario.Perfil}");

            // 6. Impedir acesso normal quando a troca
            // de senha é obrigatória.
            if (usuario.PrecisaTrocarSenha)
            {
                await DisplayAlert(
                    "Troca de senha necessária",
                    "Sua conta precisa trocar a senha antes de continuar.",
                    "OK");

                // Abre a tela de troca; ao salvar, ela abre o menu.
                entry_senha.Text = string.Empty;

                await Navigation.PushAsync(
                    new TrocarSenhaPage(usuario, App.Database));

                return;
            }

            // 7. Autenticação concluída.
            System.Diagnostics.Debug.WriteLine(
                "LOGIN: autenticação concluída");

            // 8. Abrir o menu principal.
            await Navigation.PushAsync(
     new MenuPrincipal(usuario, App.Database));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"LOGIN: erro = {ex}");

            await DisplayAlert(
                "Erro no login",
                "Não foi possível realizar o acesso. " +
                "Consulte a saída de depuração.",
                "OK");
        }
        finally
        {
            _processandoLogin = false;
        }
    }

    private async void Button_Recuperar_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(
                new RecuperarAcessoPage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"LOGIN: erro na recuperação = {ex}");

            await DisplayAlert(
                "Erro",
                $"Não foi possível abrir a recuperação: {ex.Message}",
                "OK");
        }
    }
}