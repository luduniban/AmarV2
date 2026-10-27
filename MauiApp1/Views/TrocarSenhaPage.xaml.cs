using maui_amarv.Data;
using maui_amarv.Helpers;
using maui_amarv.Models;

namespace maui_amarv.Views;

public partial class TrocarSenhaPage : ContentPage
{
    private readonly UsuarioDoSistema _usuario;
    private readonly BancoDados _banco;

    public TrocarSenhaPage(UsuarioDoSistema usuario, BancoDados banco)
    {
        InitializeComponent();

        _usuario = usuario
            ?? throw new ArgumentNullException(nameof(usuario));

        _banco = banco
            ?? throw new ArgumentNullException(nameof(banco));

        NavigationPage.SetHasBackButton(this, false);
    }

    // Impede contornar a troca obrigatória com o botão voltar.
    protected override bool OnBackButtonPressed() => true;

    private async void Button_Salvar_Clicked(object sender, EventArgs e)
    {
        try
        {
            string nova = entry_nova_senha.Text ?? string.Empty;
            string confirmar = entry_confirmar_senha.Text ?? string.Empty;

            if (nova.Length < SenhaHelper.TamanhoMinimo)
            {
                await DisplayAlert(
                    "Atenção",
                    $"A senha deve ter pelo menos {SenhaHelper.TamanhoMinimo} caracteres.",
                    "OK");
                return;
            }

            if (nova != confirmar)
            {
                await DisplayAlert(
                    "Atenção",
                    "A confirmação não confere com a nova senha.",
                    "OK");
                return;
            }

            if (SenhaHelper.Verificar(nova, _usuario.Senha))
            {
                await DisplayAlert(
                    "Atenção",
                    "A nova senha deve ser diferente da atual.",
                    "OK");
                return;
            }

            await _banco.UsuarioHelper.TrocarSenhaAsync(_usuario.Id, nova);

            _usuario.Senha = SenhaHelper.Proteger(nova);
            _usuario.PrecisaTrocarSenha = false;

            // Substitui esta página pelo menu, deixando Login → Menu na pilha.
            Navigation.InsertPageBefore(
                new MenuPrincipal(_usuario, _banco),
                this);

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ops", ex.Message, "OK");
        }
    }
}
