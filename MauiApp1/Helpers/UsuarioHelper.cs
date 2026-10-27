
using SQLite;
using maui_amarv.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace maui_amarv.Helpers
{
    public class UsuarioHelper
    {
        private readonly SQLiteAsyncConnection _database;

        public UsuarioHelper(SQLiteAsyncConnection database)
        {
            _database = database;
        }

        // ============================================================
        // BUSCAR USUÁRIO PELO LOGIN E SENHA
        // ============================================================

        public async Task<UsuarioDoSistema?> BuscarUsuario(
            string login,
            string senha)
        {
            if (string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrEmpty(senha))
                return null;

            // A senha nunca é comparada no SQL: o usuário é localizado
            // pelo login e o hash é verificado em código.
            var usuario = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Login == login && u.Ativo)
                .FirstOrDefaultAsync();

            if (usuario == null ||
                !SenhaHelper.Verificar(senha, usuario.Senha))
                return null;

            // Migra senhas antigas (texto puro) para hash no primeiro login.
            if (!SenhaHelper.EstaProtegida(usuario.Senha))
            {
                usuario.Senha = SenhaHelper.Proteger(senha);
                await _database.UpdateAsync(usuario);
            }

            await CarregarNomeAsync(usuario);

            return usuario;
        }

        // ============================================================
        // TROCAR SENHA
        // ============================================================

        public async Task TrocarSenhaAsync(
            int usuarioId,
            string novaSenha)
        {
            if (string.IsNullOrEmpty(novaSenha) ||
                novaSenha.Length < SenhaHelper.TamanhoMinimo)
            {
                throw new InvalidOperationException(
                    $"A senha deve ter pelo menos " +
                    $"{SenhaHelper.TamanhoMinimo} caracteres.");
            }

            var usuario = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Id == usuarioId)
                .FirstOrDefaultAsync();

            if (usuario == null)
                throw new InvalidOperationException(
                    "Usuário não encontrado.");

            usuario.Senha = SenhaHelper.Proteger(novaSenha);
            usuario.PrecisaTrocarSenha = false;

            await _database.UpdateAsync(usuario);
        }



        // ============================================================
        // BUSCAR USUÁRIO DO SISTEMA
        // ============================================================

        public async Task<UsuarioDoSistema?> BuscarUsuarioDoSistema(
            string login,
            string senha)
        {
            return await BuscarUsuario(login, senha);
        }

        // ============================================================
        // BUSCAR USUÁRIO PELO ID
        // ============================================================

        public async Task<UsuarioDoSistema?> BuscarPorIdAsync(int id)
        {
            if (id <= 0)
                return null;

            var usuario = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Id == id)
                .FirstOrDefaultAsync();

            if (usuario != null)
                await CarregarNomeAsync(usuario);

            return usuario;
        }

        // ============================================================
        // LISTAR USUÁRIOS
        // ============================================================

        public async Task<List<UsuarioDoSistema>> Listar()
        {
            var usuarios = await _database
                .Table<UsuarioDoSistema>()
                .ToListAsync();

            foreach (var usuario in usuarios)
                await CarregarNomeAsync(usuario);

            return usuarios;
        }

        // ============================================================
        // CARREGAR NOME A PARTIR DA TABELA PESSOA
        // ============================================================

        private async Task CarregarNomeAsync(
            UsuarioDoSistema usuario)
        {
            if (usuario.Id <= 0)
                return;

            var pessoa = await _database
                .Table<Pessoa>()
                .Where(p => p.Id == usuario.Id)
                .FirstOrDefaultAsync();

            usuario.Nome = pessoa?.Nome ?? string.Empty;
        }

        // ============================================================
        // GARANTIR REGISTRO DE PESSOA
        // ============================================================

        private async Task GarantirPessoaAsync(
            UsuarioDoSistema usuario,
            string nomePadrao)
        {
            if (usuario.Id <= 0)
            {
                throw new InvalidOperationException(
                    "O usuário precisa ter um ID válido.");
            }

            var pessoa = await _database
                .Table<Pessoa>()
                .Where(p => p.Id == usuario.Id)
                .FirstOrDefaultAsync();

            if (pessoa == null)
            {
                string nome = !string.IsNullOrWhiteSpace(usuario.Nome)
                    ? usuario.Nome.Trim()
                    : nomePadrao.Trim();

                if (string.IsNullOrWhiteSpace(nome))
                {
                    throw new InvalidOperationException(
                        "O nome da pessoa é obrigatório.");
                }

                pessoa = new Pessoa
                {
                    Id = usuario.Id,
                    Nome = nome
                };

                await _database.InsertAsync(pessoa);
            }
            else
            {
                // Não substitui um nome já cadastrado.
                if (string.IsNullOrWhiteSpace(pessoa.Nome))
                {
                    pessoa.Nome = !string.IsNullOrWhiteSpace(usuario.Nome)
                        ? usuario.Nome.Trim()
                        : nomePadrao.Trim();

                    await _database.UpdateAsync(pessoa);
                }
            }

            usuario.Nome = pessoa.Nome;
        }

        // ============================================================
        // SALVAR USUÁRIO
        // Pessoa.Id = UsuarioDoSistema.Id
        // ============================================================


        public async Task<int> Salvar(UsuarioDoSistema usuario)
        {
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario));

            if (usuario.Id <= 0)
                throw new InvalidOperationException(
                    "Selecione uma Pessoa previamente cadastrada.");

            if (string.IsNullOrWhiteSpace(usuario.Login))
                throw new InvalidOperationException("O login é obrigatório.");

            if (string.IsNullOrWhiteSpace(usuario.Perfil))
                throw new InvalidOperationException("Selecione o perfil.");

            var pessoa = await _database
                .Table<Pessoa>()
                .Where(p => p.Id == usuario.Id)
                .FirstOrDefaultAsync();

            if (pessoa == null)
                throw new InvalidOperationException(
                    "A Pessoa selecionada não está cadastrada.");

            string cpf = new string(
                (pessoa.Cpf ?? string.Empty)
                    .Where(char.IsDigit)
                    .ToArray());

            if (cpf.Length != 11)
                throw new InvalidOperationException(
                    "A Pessoa precisa ter um CPF válido cadastrado.");

            if (pessoa.DataNascimento == default ||
                pessoa.DataNascimento.Date < new DateTime(1900, 1, 1) ||
                pessoa.DataNascimento.Date > DateTime.Today)
            {
                throw new InvalidOperationException(
                    "A Pessoa precisa ter uma data de nascimento válida.");
            }

            usuario.Nome = pessoa.Nome;

            string login = usuario.Login.Trim();

            var usuarios = await _database
                .Table<UsuarioDoSistema>()
                .ToListAsync();

            bool duplicado = usuarios.Any(u =>
                string.Equals(
                    u.Login?.Trim(),
                    login,
                    StringComparison.OrdinalIgnoreCase)
                && u.Id != usuario.Id);

            if (duplicado)
                throw new InvalidOperationException(
                    "Já existe um usuário com esse login.");

            var existente = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Id == usuario.Id)
                .FirstOrDefaultAsync();

            if (existente == null && string.IsNullOrWhiteSpace(usuario.Senha))
                throw new InvalidOperationException("A senha é obrigatória.");

            if (!string.IsNullOrWhiteSpace(usuario.Senha))
            {
                if (usuario.Senha.Length < SenhaHelper.TamanhoMinimo)
                    throw new InvalidOperationException(
                        $"A senha deve ter pelo menos {SenhaHelper.TamanhoMinimo} caracteres.");

                if (!SenhaHelper.EstaProtegida(usuario.Senha))
                    usuario.Senha = SenhaHelper.Proteger(usuario.Senha);
            }
            else if (existente != null)
            {
                // Campo de senha vazio na edição: mantém a senha existente.
                usuario.Senha = existente.Senha;
            }

            usuario.Login = login;

            // O Id do usuário será sempre o Id da Pessoa selecionada.
            usuario.Id = pessoa.Id;

            if (existente == null)
                await _database.InsertAsync(usuario);
            else
                await _database.UpdateAsync(usuario);

            if (string.Equals(
                usuario.Perfil,
                "Administrador",
                StringComparison.OrdinalIgnoreCase))
            {
                await GarantirAdministradorAsync(usuario.Id);
            }

            await CarregarNomeAsync(usuario);

            return usuario.Id;
        }


        // ============================================================
        // GARANTIR REGISTRO DE ADMINISTRADOR
        // AdministradorDoSistema.Id = UsuarioDoSistema.Id
        // ============================================================

        private async Task GarantirAdministradorAsync(int usuarioId)
        {
            var administrador = await _database
                .Table<AdministradorDoSistema>()
                .Where(a => a.Id == usuarioId)
                .FirstOrDefaultAsync();

            if (administrador == null)
            {
                await _database.InsertAsync(
                    new AdministradorDoSistema
                    {
                        Id = usuarioId,
                        NivelAcesso = "Total"
                    });
            }
        }

        // ============================================================
        // EXCLUIR USUÁRIO
        // Não exclui Pessoa nem Administrador automaticamente.
        // ============================================================

        public async Task<int> Excluir(
            UsuarioDoSistema usuario)
        {
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario));

            if (usuario.Id <= 0)
                throw new InvalidOperationException(
                    "O ID do usuário é inválido.");

            var administrador = await _database
                .Table<AdministradorDoSistema>()
                .Where(a => a.Id == usuario.Id)
                .FirstOrDefaultAsync();

            if (administrador != null)
            {
                throw new InvalidOperationException(
                    "Este usuário possui registro de administrador. " +
                    "Trate o vínculo de administrador antes de excluir.");
            }

            return await _database.DeleteAsync(usuario);
        }

        // ============================================================
        // CARREGAR USUÁRIO INICIAL
        // Só cria a conta de administrador quando NÃO existe nenhum
        // usuário. Contas existentes nunca são alteradas aqui (antes,
        // cada inicialização reativava e re-promovia admin/usuario).
        // A senha inicial é provisória: a troca é obrigatória no
        // primeiro acesso.
        // ============================================================



        private async Task CriarContaInicialAsync(
            string nome,
            string cpf,
            DateTime nascimento,
            string login,
            string perfil)
        {
            var pessoa = new Pessoa
            {
                Nome = nome,
                Cpf = cpf,
                DataNascimento = nascimento
            };

            await _database.InsertAsync(pessoa);

            await _database.InsertAsync(new UsuarioDoSistema
            {
                Id = pessoa.Id,
                Login = login,
                Senha = SenhaHelper.Proteger("123"),
                Perfil = perfil,
                Status = "Ativo",
                Ativo = true,
                PrecisaTrocarSenha = true
            });

            if (perfil == "Administrador")
                await GarantirAdministradorAsync(pessoa.Id);
        }

        public async Task CarregarUsuariosIniciais()
        {
            var usuarios = await _database
                .Table<UsuarioDoSistema>()
                .ToListAsync();

            // Banco vazio (primeira execução): cria as contas iniciais
            // (admin e usuario, senha provisória "123"). A troca da senha
            // é obrigatória no primeiro acesso (ver LoginPage).
            if (usuarios.Count == 0)
            {
                await CriarContaInicialAsync(
                    nome: "Administrador",
                    cpf: "00000000000",
                    nascimento: new DateTime(1980, 1, 1),
                    login: "admin",
                    perfil: "Administrador");

                await CriarContaInicialAsync(
                    nome: "Usuário de Teste",
                    cpf: "11111111111",
                    nascimento: new DateTime(1990, 1, 1),
                    login: "usuario",
                    perfil: "Usuario");

                usuarios = await _database
                    .Table<UsuarioDoSistema>()
                    .ToListAsync();
            }

            foreach (var usuario in usuarios)
            {
                if (usuario.Id <= 0)
                {
                    throw new InvalidOperationException(
                        $"O usuário '{usuario.Login}' possui um ID inválido.");
                }

                var pessoa = await _database
                    .Table<Pessoa>()
                    .Where(p => p.Id == usuario.Id)
                    .FirstOrDefaultAsync();

                if (pessoa == null)
                {
                    throw new InvalidOperationException(
                        $"O usuário '{usuario.Login}' não possui uma Pessoa vinculada. " +
                        "Verifique o cadastro antes de continuar.");
                }

                await CarregarNomeAsync(usuario);
            }
        }

        public async Task<string> DiagnosticarLoginAsync(string login)
        {
            var usuario = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Login == login)
                .FirstOrDefaultAsync();

            if (usuario == null)
                return "DIAGNÓSTICO: login não encontrado no SQLite utilizado pelo aplicativo.";

            bool hashProtegido = SenhaHelper.EstaProtegida(usuario.Senha);

            return
                $"Login encontrado: {usuario.Login}\n" +
                $"ID: {usuario.Id}\n" +
                $"Perfil: {usuario.Perfil}\n" +
                $"Status: {usuario.Status}\n" +
                $"Ativo: {usuario.Ativo}\n" +
                $"Precisa trocar senha: {usuario.PrecisaTrocarSenha}\n" +
                $"Hash reconhecido: {hashProtegido}";
        }



        public async Task<UsuarioDoSistema?> RecuperarAcessoAsync(
            string cpf,
            DateTime dataNascimento,
            string novaSenha)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return null;

            if (string.IsNullOrWhiteSpace(novaSenha) ||
                novaSenha.Length < SenhaHelper.TamanhoMinimo)
            {
                throw new InvalidOperationException(
                    $"A senha deve ter pelo menos {SenhaHelper.TamanhoMinimo} caracteres.");
            }

            string cpfLimpo = new string(
                cpf.Where(char.IsDigit).ToArray());

            if (cpfLimpo.Length != 11 ||
                dataNascimento == default ||
                dataNascimento.Date > DateTime.Today)
            {
                return null;
            }

            // Consulta a pessoa pelo CPF diretamente no banco.
            var pessoas = await _database
                .Table<Pessoa>()
                .ToListAsync();

            var pessoa = pessoas.FirstOrDefault(p =>
                new string(
                    (p.Cpf ?? string.Empty)
                        .Where(char.IsDigit)
                        .ToArray()) == cpfLimpo);

            if (pessoa == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "RECUPERAÇÃO: pessoa não encontrada pelo CPF.");
                return null;
            }

            // A data já vem convertida pelo SQLite-net (o banco guarda
            // DateTime como número de ticks, não como texto).
            DateTime dataBanco = pessoa.DataNascimento;

            System.Diagnostics.Debug.WriteLine(
                $"RECUPERAÇÃO: data no banco = {dataBanco:yyyy-MM-dd}");

            System.Diagnostics.Debug.WriteLine(
                $"RECUPERAÇÃO: data informada = {dataNascimento:yyyy-MM-dd}");

            if (dataBanco.Date != dataNascimento.Date)
            {
                System.Diagnostics.Debug.WriteLine(
                    "RECUPERAÇÃO: as datas são diferentes.");
                return null;
            }

            var usuario = await _database
                .Table<UsuarioDoSistema>()
                .Where(u => u.Id == pessoa.Id)
                .FirstOrDefaultAsync();

            if (usuario == null || !usuario.Ativo)
            {
                System.Diagnostics.Debug.WriteLine(
                    "RECUPERAÇÃO: usuário inexistente ou inativo.");
                return null;
            }

            usuario.Senha = SenhaHelper.Proteger(novaSenha);
            usuario.PrecisaTrocarSenha = false;

            await _database.UpdateAsync(usuario);
            await CarregarNomeAsync(usuario);

            System.Diagnostics.Debug.WriteLine(
                "RECUPERAÇÃO: senha alterada com sucesso.");

            return usuario;
        }

        public class DataNascimentoConsulta
        {
            public string DataNascimento { get; set; } = string.Empty;
        }

    }
}
