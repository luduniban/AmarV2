
using maui_amarv.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace maui_amarv.Views;

public partial class GerenciarUsuariosPage : ContentPage
{
    private UsuarioDoSistema? usuarioSelecionado;
    private List<Pessoa> pessoas = new();

    public GerenciarUsuariosPage()
    {
        InitializeComponent();
        ConfigurarToqueAtivo();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await CarregarPessoas();
            await CarregarUsuarios();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private void ConfigurarToqueAtivo()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            chk_ativo.IsChecked = !chk_ativo.IsChecked;
        };

        linha_ativo.GestureRecognizers.Add(tap);
    }

    private async Task CarregarPessoas()
    {
        // Este método precisa existir no BancoDados.cs.
        pessoas = await App.Database.ListarPessoasAsync();

        pck_pessoa.ItemsSource = pessoas;
    }

    private async Task CarregarUsuarios()
    {
        collection_usuarios.ItemsSource =
            await App.Database.ListarUsuarios();
    }

    private void Pck_Pessoa_SelectedIndexChanged(
        object sender, EventArgs e)
    {
        if (pck_pessoa.SelectedItem is not Pessoa pessoa)
        {
            entry_nome.Text = "";
            lbl_cpf.Text = "Selecione uma Pessoa";
            lbl_data_nascimento.Text = "Selecione uma Pessoa";
            return;
        }

        entry_nome.Text = pessoa.Nome;

        string cpf = new string(
            (pessoa.Cpf ?? string.Empty)
                .Where(char.IsDigit)
                .ToArray());

        lbl_cpf.Text = cpf.Length == 11
            ? cpf
            : "CPF inválido ou não cadastrado";

        lbl_data_nascimento.Text =
            pessoa.DataNascimento == default
                ? "Data não cadastrada"
                : pessoa.DataNascimento.ToString("dd/MM/yyyy");
    }

    private bool ValidarCampos()
    {
        if (pck_pessoa.SelectedItem is not Pessoa pessoa)
        {
            _ = DisplayAlert(
                "Atenção",
                "Selecione uma Pessoa previamente cadastrada.",
                "OK");
            return false;
        }

        string cpf = new string(
            (pessoa.Cpf ?? string.Empty)
                .Where(char.IsDigit)
                .ToArray());

        if (cpf.Length != 11)
        {
            _ = DisplayAlert(
                "Atenção",
                "A Pessoa precisa ter um CPF válido cadastrado.",
                "OK");
            return false;
        }

        if (pessoa.DataNascimento == default ||
            pessoa.DataNascimento.Date < new DateTime(1900, 1, 1) ||
            pessoa.DataNascimento.Date > DateTime.Today)
        {
            _ = DisplayAlert(
                "Atenção",
                "A Pessoa precisa ter uma data de nascimento válida.",
                "OK");
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry_login.Text))
        {
            _ = DisplayAlert("Atenção", "Digite o login.", "OK");
            return false;
        }

        bool senhaObrigatoria = usuarioSelecionado == null;

        if (senhaObrigatoria &&
            string.IsNullOrWhiteSpace(entry_senha.Text))
        {
            _ = DisplayAlert("Atenção", "Digite a senha.", "OK");
            return false;
        }

        if (!string.IsNullOrEmpty(entry_senha.Text) &&
            entry_senha.Text.Length <
                maui_amarv.Helpers.SenhaHelper.TamanhoMinimo)
        {
            _ = DisplayAlert(
                "Atenção",
                $"A senha deve ter pelo menos " +
                $"{maui_amarv.Helpers.SenhaHelper.TamanhoMinimo} caracteres.",
                "OK");
            return false;
        }

        if (pck_perfil.SelectedItem == null)
        {
            _ = DisplayAlert("Atenção", "Selecione o perfil.", "OK");
            return false;
        }

        return true;
    }

    private async void Button_Salvar_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (!ValidarCampos())
                return;

            Pessoa pessoa = (Pessoa)pck_pessoa.SelectedItem;

            var usuario = new UsuarioDoSistema
            {
                Id = pessoa.Id,
                Nome = pessoa.Nome,
                Login = entry_login.Text.Trim(),
                Senha = entry_senha.Text,
                Perfil = pck_perfil.SelectedItem!.ToString()!,
                Status = chk_ativo.IsChecked ? "Ativo" : "Inativo",
                Ativo = chk_ativo.IsChecked,
                PrecisaTrocarSenha = true
            };

            await App.Database.SalvarUsuario(usuario);

            await DisplayAlert(
                "Sucesso",
                "Usuário cadastrado e vinculado à Pessoa.",
                "OK");

            LimparCampos();
            await CarregarUsuarios();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro ao salvar", ex.Message, "OK");
        }
    }

    private async void Button_Atualizar_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (usuarioSelecionado == null)
            {
                await DisplayAlert(
                    "Atenção",
                    "Selecione Editar em um usuário da lista.",
                    "OK");
                return;
            }

            if (!ValidarCampos())
                return;

            Pessoa pessoa = (Pessoa)pck_pessoa.SelectedItem;

            // Impede transferir um acesso para outra Pessoa durante a edição.
            if (pessoa.Id != usuarioSelecionado.Id)
            {
                await DisplayAlert(
                    "Atenção",
                    "A edição não pode alterar a Pessoa vinculada ao usuário.",
                    "OK");
                return;
            }

            usuarioSelecionado.Nome = pessoa.Nome;
            usuarioSelecionado.Login = entry_login.Text.Trim();

            if (!string.IsNullOrWhiteSpace(entry_senha.Text))
            {
                usuarioSelecionado.Senha = entry_senha.Text;
                usuarioSelecionado.PrecisaTrocarSenha = true;
            }

            usuarioSelecionado.Perfil =
                pck_perfil.SelectedItem!.ToString()!;
            usuarioSelecionado.Status =
                chk_ativo.IsChecked ? "Ativo" : "Inativo";
            usuarioSelecionado.Ativo = chk_ativo.IsChecked;

            await App.Database.AtualizarUsuario(usuarioSelecionado);

            await DisplayAlert("Sucesso", "Usuário atualizado.", "OK");

            LimparCampos();
            usuarioSelecionado = null;
            await CarregarUsuarios();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro ao atualizar", ex.Message, "OK");
        }
    }

    private async void Button_Editar_Item_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button botao ||
            botao.CommandParameter is not UsuarioDoSistema usuario)
            return;

        usuarioSelecionado = usuario;

        var pessoa = pessoas.FirstOrDefault(p => p.Id == usuario.Id);

        if (pessoa == null)
        {
            await DisplayAlert(
                "Atenção",
                "A Pessoa vinculada não foi encontrada. Verifique o banco.",
                "OK");
            return;
        }

        pck_pessoa.SelectedItem = pessoa;
        entry_nome.Text = pessoa.Nome;
        entry_login.Text = usuario.Login;
        entry_senha.Text = "";

        pck_perfil.SelectedIndex =
            usuario.Perfil == "Administrador" ? 0 :
            usuario.Perfil == "Usuario" ? 1 : -1;

        chk_ativo.IsChecked = usuario.Ativo;
    }

    private async Task AlterarEstadoUsuario(
        Button botao, bool ativo)
    {
        if (botao.CommandParameter is not UsuarioDoSistema usuario)
            return;

        usuario.Ativo = ativo;
        usuario.Status = ativo ? "Ativo" : "Inativo";

        await App.Database.AtualizarUsuario(usuario);
        await CarregarUsuarios();
    }

    private async void Button_Ativar_Item_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button botao)
                await AlterarEstadoUsuario(botao, true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private async void Button_Desativar_Item_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is not Button botao ||
                botao.CommandParameter is not UsuarioDoSistema)
                return;

            bool confirmar = await DisplayAlert(
                "Confirmar",
                "Deseja desativar este usuário?",
                "Sim",
                "Não");

            if (confirmar)
                await AlterarEstadoUsuario(botao, false);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }
    }

    private async void Button_Deletar_Item_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is not Button botao ||
                botao.CommandParameter is not UsuarioDoSistema usuario)
                return;

            bool confirmar = await DisplayAlert(
                "Confirmar exclusão",
                $"Deseja excluir o acesso de {usuario.Nome}?",
                "Sim",
                "Não");

            if (!confirmar)
                return;

            await App.Database.ExcluirUsuario(usuario);

            if (usuarioSelecionado?.Id == usuario.Id)
            {
                usuarioSelecionado = null;
                LimparCampos();
            }

            await CarregarUsuarios();
            await DisplayAlert("Sucesso", "Acesso excluído.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Não foi possível excluir", ex.Message, "OK");
        }
    }

    private void Button_Limpar_Clicked(object sender, EventArgs e)
    {
        LimparCampos();
        usuarioSelecionado = null;
    }

    private void LimparCampos()
    {
        pck_pessoa.SelectedItem = null;
        entry_nome.Text = "";
        lbl_cpf.Text = "Selecione uma Pessoa";
        lbl_data_nascimento.Text = "Selecione uma Pessoa";
        entry_login.Text = "";
        entry_senha.Text = "";
        pck_perfil.SelectedIndex = -1;
        chk_ativo.IsChecked = true;
    }

    private async void Button_Menu_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
