using maui_amarv.Helpers;
using maui_amarv.Models;
using Microsoft.Maui.Storage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace maui_amarv.Data
{
    public class BancoDados
    {
        private readonly SQLiteAsyncConnection _database;

        // ============================================================
        // HELPERS EXISTENTES
        // ============================================================

        public PacienteHelper PacienteHelper { get; private set; }
        public HistoricoVacinalHelper HistoricoVacinalHelper { get; private set; }
        public SincronizacaoHelper SincronizacaoHelper { get; private set; }
        public BackupHelper BackupHelper { get; private set; }

        // ============================================================
        // DEMAIS HELPERS
        // ============================================================

        public UsuarioHelper UsuarioHelper { get; private set; }
        public SessaoHelper SessaoHelper { get; private set; }
        public CondicaoClinicaHelper CondicaoClinicaHelper { get; private set; }
        public VacinaHelper VacinaHelper { get; private set; }
        public DoseHelper DoseHelper { get; private set; }
        public RegraVacinalHelper RegraVacinalHelper { get; private set; }
        public AnaliseEsquemaVacinalHelper AnaliseEsquemaVacinalHelper { get; private set; }
        public RelatorioHelper RelatorioHelper { get; private set; }

        // ============================================================
        // LISTAR PESSOAS
        // ============================================================

        public Task<List<Pessoa>> ListarPessoasAsync()
        {
            return _database.Table<Pessoa>().ToListAsync();
        }

        // ============================================================
        // COMPATIBILIDADE
        // ============================================================

        public BackupHelper Backup => BackupHelper;

        // ============================================================
        // CONTROLE DA INICIALIZAÇÃO
        // ============================================================

        private Task? _inicializacaoBanco;

        // ============================================================
        // CONSTRUTOR
        // ============================================================

        // Caminho do arquivo SQLite (usado para enviar o banco ao servidor).
        public string CaminhoBanco { get; }

        // Fecha a conexão com o arquivo do banco (antes de substituí-lo
        // por um backup restaurado). Uma nova conexão abre sozinha depois.
        public Task FecharConexaoAsync()
        {
            return _database.CloseAsync();
        }

        public BancoDados(string caminhoBanco)
        {
            CaminhoBanco = caminhoBanco;

            _database = new SQLiteAsyncConnection(caminhoBanco);

            PacienteHelper = new PacienteHelper(_database);
            HistoricoVacinalHelper = new HistoricoVacinalHelper(_database);
            SincronizacaoHelper = new SincronizacaoHelper(_database);
            BackupHelper = new BackupHelper(_database);

            UsuarioHelper = new UsuarioHelper(_database);
            SessaoHelper = new SessaoHelper(_database);
            CondicaoClinicaHelper = new CondicaoClinicaHelper(_database);
            VacinaHelper = new VacinaHelper(_database);
            DoseHelper = new DoseHelper(_database);
            RegraVacinalHelper = new RegraVacinalHelper(_database);

            AnaliseEsquemaVacinalHelper =
                new AnaliseEsquemaVacinalHelper(_database);

            RelatorioHelper = new RelatorioHelper(_database);
        }

        // ============================================================
        // INICIALIZAÇÃO DO BANCO
        // ============================================================

        private readonly object _travaInicializacao = new();

        public Task InicializarAsync()
        {
            lock (_travaInicializacao)
            {
                // Se a tentativa anterior falhou, tenta de novo em vez de
                // devolver para sempre a mesma Task com erro.
                if (_inicializacaoBanco == null ||
                    _inicializacaoBanco.IsFaulted ||
                    _inicializacaoBanco.IsCanceled)
                {
                    _inicializacaoBanco = CriarTabelasAsync();
                }

                return _inicializacaoBanco;
            }
        }

        // ============================================================
        // GARANTIR COLUNA NOMEPACIENTE
        // ============================================================

        private async Task GarantirColunaNomePacienteAsync()
        {
            try
            {
                var colunas =
                    await _database.QueryAsync<ColunaSQLite>(
                        "PRAGMA table_info('AnaliseEsquemaVacinal');");

                bool existe = colunas.Any(c =>
                    string.Equals(
                        c.Name,
                        "NomePaciente",
                        StringComparison.OrdinalIgnoreCase));

                if (!existe)
                {
                    await _database.ExecuteAsync(
                        "ALTER TABLE AnaliseEsquemaVacinal " +
                        "ADD COLUMN NomePaciente TEXT NOT NULL DEFAULT '';");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Erro ao verificar NomePaciente: {ex}");
            }
        }

        // ============================================================
        // EXIBIR ESTRUTURA DAS TABELAS
        // ============================================================

        private async Task ExibirEstruturaTabelasAsync()
        {
            try
            {
                string[] tabelas =
                {
                    "Pessoa",
                    "Paciente",
                    "UsuarioDoSistema",
                    "AdministradorDoSistema",
                    "Sessao",
                    "HistoricoVacinal"
                };

                foreach (string tabela in tabelas)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"========== ESTRUTURA: {tabela} ==========");

                    var colunas =
                        await _database.QueryAsync<ColunaSQLite>(
                            $"PRAGMA table_info(\"{tabela}\");");

                    foreach (var coluna in colunas)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Coluna: {coluna.Name} | " +
                            $"Tipo: {coluna.Type} | " +
                            $"PK: {coluna.PrimaryKey} | " +
                            $"Obrigatória: {coluna.NotNull}");
                    }

                    var chaves =
                        await _database.QueryAsync<ChaveEstrangeiraSQLite>(
                            $"PRAGMA foreign_key_list(\"{tabela}\");");

                    foreach (var chave in chaves)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"FK: {chave.From} -> " +
                            $"{chave.Table}.{chave.To}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Erro ao exibir a estrutura das tabelas: {ex}");
            }
        }

        // ============================================================
        // CRIAÇÃO DAS TABELAS E DIAGNÓSTICO
        // ============================================================

        private async Task CriarTabelasAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    "### DIAGNÓSTICO: entrou em CriarTabelasAsync ###");

                // 1. Habilitar chaves estrangeiras nesta conexão.

                await _database.ExecuteAsync(
                    "PRAGMA foreign_keys = ON;");

                int foreignKeysInicial =
                    await _database.ExecuteScalarAsync<int>(
                        "PRAGMA foreign_keys;");

                System.Diagnostics.Debug.WriteLine(
                    $"SQLite: foreign_keys inicial = {foreignKeysInicial}");

                // 2. Tabelas principais.
               

                // Primeiro, corrige a estrutura da tabela Pessoa existente.
                  System.Diagnostics.Debug.WriteLine(
                  "PASSO 1: iniciando migração DataNascimento");

                await GarantirColunaDataNascimentoPessoaAsync();

                System.Diagnostics.Debug.WriteLine(
                    "PASSO 2: migração DataNascimento concluída");

                // Depois, cria ou verifica a tabela Pessoa.
                System.Diagnostics.Debug.WriteLine(
                    "PASSO 3: criando/verificando tabela Pessoa");

                await _database.CreateTableAsync<Pessoa>();

                System.Diagnostics.Debug.WriteLine(
                    "PASSO 4: tabela Pessoa concluída");


                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela UsuarioDoSistema");
                await _database.CreateTableAsync<UsuarioDoSistema>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela AdministradorDoSistema");
                await _database.CreateTableAsync<AdministradorDoSistema>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Paciente");
                await _database.CreateTableAsync<Paciente>();

                // 3. Demais tabelas.
                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Sessao");
                await _database.CreateTableAsync<Sessao>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela CondicaoClinica");
                await _database.CreateTableAsync<CondicaoClinica>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Vacina");
                await _database.CreateTableAsync<Vacina>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Dose");
                await _database.CreateTableAsync<Dose>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela CalendarioVacinal");
                await _database.CreateTableAsync<CalendarioVacinal>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela CalendarioVacinalSUS");
                await _database.CreateTableAsync<CalendarioVacinalSUS>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela CalendarioVacinalRedePrivada");
                await _database.CreateTableAsync<CalendarioVacinalRedePrivada>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela RegraVacinal");
                await _database.CreateTableAsync<RegraVacinal>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela HistoricoVacinal");
                await _database.CreateTableAsync<HistoricoVacinal>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela AnaliseEsquemaVacinal");
                await _database.CreateTableAsync<AnaliseEsquemaVacinal>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Relatorio");
                await _database.CreateTableAsync<Relatorio>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela RelatorioVacinalPaciente");
                await _database.CreateTableAsync<RelatorioVacinalPaciente>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela RelatorioPacientesEmSituacaoRegular");
                await _database.CreateTableAsync<RelatorioPacientesEmSituacaoRegular>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela RelatorioPacienteComVacinasAtrasadas");
                await _database.CreateTableAsync<RelatorioPacienteComVacinasAtrasadas>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Sincronizacao");
                await _database.CreateTableAsync<Sincronizacao>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: tabela Backup");
                await _database.CreateTableAsync<Backup>();

                System.Diagnostics.Debug.WriteLine("INICIANDO: migração NomePaciente");
                await GarantirColunaNomePacienteAsync();

                System.Diagnostics.Debug.WriteLine("INICIANDO: limpeza dos históricos");
                await LimparHistoricosSemDose();

                System.Diagnostics.Debug.WriteLine("CONCLUÍDO: tabelas e migrações");

                // Mantém o método de limpeza já existente no projeto.
                await LimparHistoricosSemDose();

#if DEBUG
                // 5. Listar tabelas reais do SQLite (somente diagnóstico).

                var tabelas =
                    await _database.QueryAsync<TabelaSQLite>(
                        @"SELECT name
                          FROM sqlite_master
                          WHERE type = 'table'
                          ORDER BY name;");

                System.Diagnostics.Debug.WriteLine(
                    "========== TABELAS REAIS DO SQLITE ==========");

                foreach (var tabela in tabelas)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Tabela encontrada: {tabela.Name}");
                }

                // 6. Exibir colunas e chaves estrangeiras.

                await ExibirEstruturaTabelasAsync();

                System.Diagnostics.Debug.WriteLine(
                    "### DIAGNÓSTICO: terminou ExibirEstruturaTabelasAsync ###");

                // 7. Verificar integridade referencial.

                int foreignKeysAtivas =
                    await _database.ExecuteScalarAsync<int>(
                        "PRAGMA foreign_keys;");

                System.Diagnostics.Debug.WriteLine(
                    $"### DIAGNÓSTICO: foreign_keys = {foreignKeysAtivas} ###");

                var violacoes =
                    await _database.QueryAsync<ResultadoIntegridadeSQLite>(
                        "PRAGMA foreign_key_check;");

                System.Diagnostics.Debug.WriteLine(
                    $"SQLite: violações de chaves estrangeiras = {violacoes.Count}");

                foreach (var violacao in violacoes)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Violação: tabela={violacao.Table}, " +
                        $"registro={violacao.RowId}, " +
                        $"tabela referenciada={violacao.Parent}, " +
                        $"FK={violacao.ForeignKeyId}");
                }

#endif

                // 8. Carregar usuários iniciais.

                await UsuarioHelper.CarregarUsuariosIniciais();

                System.Diagnostics.Debug.WriteLine(
                    "### DIAGNÓSTICO: carregamento de usuários concluído ###");

                // 9. Verificar versão do calendário.

                const string versaoCalendario = VersaoCalendario;

                string versaoAtual = Preferences.Get(
                    "AmarV_Calendario_Versao",
                    "");

                int totalRegras = await _database
                    .Table<RegraVacinal>()
                    .CountAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"Calendário: versão salva = '{versaoAtual}'");

                System.Diagnostics.Debug.WriteLine(
                    $"Calendário: regras existentes = {totalRegras}");

                if (versaoAtual != versaoCalendario || totalRegras == 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "### DIAGNÓSTICO: carregando regras iniciais ###");

                    // Mantém o método de carregamento existente.
                    await CarregarRegrasIniciais();

                    Preferences.Set(
                        "AmarV_Calendario_Versao",
                        versaoCalendario);
                }

                System.Diagnostics.Debug.WriteLine(
                    "### TESTE: CriarTabelasAsync chegou ao final ###");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "### ERRO AO INICIALIZAR O BANCO SQLITE ###");

                System.Diagnostics.Debug.WriteLine(ex.ToString());

                throw;
            }
        }

        // ============================================================
        // GARANTIR COLUNA DATANASCIMENTO
        // ============================================================

        private async Task GarantirColunaDataNascimentoPessoaAsync()
        {
            // Verifica se a tabela Pessoa já existe.
            var tabela = await _database.QueryAsync<ColunaSQLite>(
                "SELECT name AS Name FROM sqlite_master " +
                "WHERE type = 'table' AND name = 'Pessoa';");

            // Na primeira execução, a tabela ainda será criada normalmente.
            if (tabela.Count == 0)
                return;

            // Verifica se DataNascimento já existe.
            var colunas = await _database.QueryAsync<ColunaSQLite>(
                "PRAGMA table_info('Pessoa');");

            bool existe = colunas.Any(c =>
                string.Equals(
                    c.Name,
                    "DataNascimento",
                    StringComparison.OrdinalIgnoreCase));

            if (!existe)
            {
                await _database.ExecuteAsync(
                    "ALTER TABLE Pessoa " +
                    "ADD COLUMN DataNascimento TEXT " +
                    "NOT NULL DEFAULT '1900-01-01T00:00:00';");

                System.Diagnostics.Debug.WriteLine(
                    "Migração concluída: Pessoa.DataNascimento adicionada.");
            }
        }

        // ============================================================
        // CLASSE AUXILIAR: TABELAS DO SQLITE
        // CORRIGE O ERRO CS0246 DE TabelaSQLite
        // ============================================================

        private class TabelaSQLite
        {
            [Column("name")]
            public string Name { get; set; } = string.Empty;
        }

        // ============================================================
        // CLASSE AUXILIAR: RESULTADO DA VERIFICAÇÃO DE INTEGRIDADE
        // ============================================================

        private class ResultadoIntegridadeSQLite
        {
            [Column("table")]
            public string Table { get; set; } = string.Empty;

            [Column("rowid")]
            public long? RowId { get; set; }

            [Column("parent")]
            public string Parent { get; set; } = string.Empty;

            [Column("fkid")]
            public int ForeignKeyId { get; set; }
        }

        // ============================================================
        // CLASSE AUXILIAR: CHAVES ESTRANGEIRAS
        // ============================================================

        private class ChaveEstrangeiraSQLite
        {
            [Column("id")]
            public int Id { get; set; }

            [Column("seq")]
            public int Seq { get; set; }

            [Column("table")]
            public string Table { get; set; } = string.Empty;

            [Column("from")]
            public string From { get; set; } = string.Empty;

            [Column("to")]
            public string To { get; set; } = string.Empty;
        }

        // ============================================================
        // CLASSE AUXILIAR: COLUNAS DO SQLITE
        // ============================================================

        private class ColunaSQLite
        {
            [Column("cid")]
            public int Cid { get; set; }

            [Column("name")]
            public string Name { get; set; } = string.Empty;

            [Column("type")]
            public string Type { get; set; } = string.Empty;

            [Column("notnull")]
            public int NotNull { get; set; }

            [Column("dflt_value")]
            public string? DefaultValue { get; set; }

            [Column("pk")]
            public int PrimaryKey { get; set; }
        }


// ============================================================
// USUÁRIOS
// ============================================================

public async Task<UsuarioDoSistema?> BuscarUsuario(
            string login,
            string senha)
        {
            return await UsuarioHelper.BuscarUsuario(
                login,
                senha);
        }
        public async Task<string> DiagnosticarLoginAsync(string login)
        {
            return await UsuarioHelper.DiagnosticarLoginAsync(login);
        }

        public async Task<UsuarioDoSistema?> BuscarUsuarioDoSistema(
            string login,
            string senha)
        {
            return await UsuarioHelper.BuscarUsuarioDoSistema(
                login,
                senha);
        }

        public async Task<List<UsuarioDoSistema>> ListarUsuarios()
        {
            return await UsuarioHelper.Listar();
        }

        public async Task CarregarUsuariosIniciais()
        {
            await UsuarioHelper.CarregarUsuariosIniciais();
        }

        public async Task<int> SalvarUsuario(
            UsuarioDoSistema usuario)
        {
            return await UsuarioHelper.Salvar(usuario);
        }

        public async Task<int> AtualizarUsuario(
            UsuarioDoSistema usuario)
        {
            return await UsuarioHelper.Salvar(usuario);
        }

        public async Task<int> ExcluirUsuario(
            UsuarioDoSistema usuario)
        {
            return await UsuarioHelper.Excluir(usuario);
        }


        public async Task<UsuarioDoSistema?> RecuperarAcessoAsync(
            string cpf,
            DateTime dataNascimento,
            string novaSenha)
        {
            return await UsuarioHelper.RecuperarAcessoAsync(
                cpf,
                dataNascimento,
                novaSenha);
        }

        // ============================================================
        // SESSÃO
        // ============================================================

        public async Task<int> SalvarSessao(Sessao sessao)
        {
            return await SessaoHelper.Salvar(sessao);
        }

        public async Task<Sessao?> ObterSessaoAtual()
        {
            return await SessaoHelper.ObterAtual();
        }

        public async Task LimparSessoes()
        {
            await SessaoHelper.Limpar();
        }


        // ============================================================
        // PEssoa salvar
        // ============================================================

        public async Task<int> SalvarPessoa(Pessoa pessoa)
        {
            if (pessoa == null)
                throw new ArgumentNullException(nameof(pessoa));

            if (string.IsNullOrWhiteSpace(pessoa.Nome))
                throw new InvalidOperationException("O nome é obrigatório.");

            string cpf = new string(
                (pessoa.Cpf ?? string.Empty)
                .Where(char.IsDigit)
                .ToArray());

            if (cpf.Length != 11)
                throw new InvalidOperationException(
                    "O CPF deve conter 11 dígitos.");

            // Impede o cadastro de duas pessoas com o mesmo CPF.
            var pessoas = await _database.Table<Pessoa>().ToListAsync();

            var existente = pessoas.FirstOrDefault(p =>
                new string((p.Cpf ?? string.Empty)
                    .Where(char.IsDigit)
                    .ToArray()) == cpf);

            if (existente != null && existente.Id != pessoa.Id)
                throw new InvalidOperationException(
                    "Já existe uma pessoa cadastrada com esse CPF.");

            pessoa.Cpf = cpf;

            if (pessoa.Id > 0)
            {
                await _database.UpdateAsync(pessoa);
                return pessoa.Id;
            }

            await _database.InsertAsync(pessoa);

            return pessoa.Id;
        }


        // ============================================================
        // excluir pessoa
        // ============================================================

        public async Task ExcluirPessoa(Pessoa pessoa)
        {
            if (pessoa == null || pessoa.Id <= 0)
            {
                throw new InvalidOperationException(
                    "Selecione uma pessoa válida para excluir.");
            }

            // Impede excluir uma pessoa que esteja cadastrada
            // também como paciente.
            var paciente = await _database
                .Table<Paciente>()
                .Where(p => p.Id == pessoa.Id)
                .FirstOrDefaultAsync();

            if (paciente != null)
            {
                throw new InvalidOperationException(
                    "Esta pessoa está vinculada a um paciente. " +
                    "Remova ou trate esse vínculo antes de excluir.");
            }

            await _database.DeleteAsync(pessoa);
        }

        // ============================================================
        // editar pessoa
        // ============================================================

     
public async Task<int> EditarPessoa(Pessoa pessoa)
        {
            if (pessoa == null)
                throw new ArgumentNullException(nameof(pessoa));

            if (pessoa.Id <= 0)
                throw new ArgumentException(
                    "O ID da pessoa é inválido.");

            if (string.IsNullOrWhiteSpace(pessoa.Nome))
                throw new ArgumentException(
                    "O nome da pessoa é obrigatório.");

            string cpf = new string(
                (pessoa.Cpf ?? string.Empty)
                    .Where(char.IsDigit)
                    .ToArray());

            if (cpf.Length != 11)
                throw new ArgumentException(
                    "O CPF deve conter 11 dígitos.");

            int resultado = 0;

            Action<SQLiteConnection> action = conn =>
            {
                // Localiza a pessoa pelo ID original.
                Pessoa? pessoaBanco = conn.Find<Pessoa>(pessoa.Id);

                if (pessoaBanco == null)
                    throw new InvalidOperationException(
                        "Pessoa não encontrada para edição.");

                // Verifica se o CPF já pertence a outra pessoa.
                var pessoasBanco = conn.Table<Pessoa>().ToList();

                bool cpfDuplicado = pessoasBanco.Any(p =>
                    p.Id != pessoa.Id &&
                    new string(
                        (p.Cpf ?? string.Empty)
                            .Where(char.IsDigit)
                            .ToArray()) == cpf);

                if (cpfDuplicado)
                    throw new InvalidOperationException(
                        "Já existe outra pessoa cadastrada com este CPF.");

                // Atualiza os dados sem alterar a chave primária.
                pessoaBanco.Nome = pessoa.Nome.Trim();
                pessoaBanco.Cpf = cpf;

                pessoaBanco.Nome_da_Mae =
                    pessoa.Nome_da_Mae?.Trim() ?? string.Empty;

                pessoaBanco.Telefone =
                    pessoa.Telefone?.Trim() ?? string.Empty;

                pessoaBanco.Endereco =
                    pessoa.Endereco?.Trim() ?? string.Empty;

                if (pessoa.DataNascimento.Year >= 1900 &&
    pessoa.DataNascimento.Date <= DateTime.Today)
                {
                    pessoaBanco.DataNascimento = pessoa.DataNascimento.Date;
                }


                // Executa a atualização e guarda o resultado.
                resultado = conn.Update(pessoaBanco);
            };

            // Executa a atualização dentro da transação SQLite.
            await _database.RunInTransactionAsync(action);

            // Retorna a quantidade de registros atualizados.
            return resultado;
        }

        // ============================================================
        // PACIENTES
        // ============================================================

        public async Task<List<Paciente>> ListarPacientes()
        {
            return await PacienteHelper.Listar();
        }

        public async Task<Paciente?> BuscarPaciente(int id)
        {
            return await _database
                .Table<Paciente>()
                .Where(p => p.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SalvarPaciente(
            Paciente paciente,
            Pessoa pessoa)
        {
            if (paciente == null)
                throw new ArgumentNullException(nameof(paciente));

            if (pessoa == null)
                throw new ArgumentNullException(nameof(pessoa));

            if (pessoa.Id <= 0)
                throw new ArgumentException(
                    "Selecione uma pessoa previamente cadastrada.");

            if (string.IsNullOrWhiteSpace(pessoa.Nome))
                throw new ArgumentException(
                    "O nome da pessoa é obrigatório.");

            string cpf = new string(
                (pessoa.Cpf ?? string.Empty)
                    .Where(char.IsDigit)
                    .ToArray());

            if (cpf.Length != 11)
                throw new ArgumentException(
                    "O CPF deve conter 11 dígitos.");

            // O paciente utiliza o mesmo ID da pessoa.
            if (paciente.Id != 0 && paciente.Id != pessoa.Id)
                throw new InvalidOperationException(
                    "O ID do paciente deve ser igual ao ID da pessoa.");

            paciente.Id = pessoa.Id;


            await _database.RunInTransactionAsync(conn =>
            {
                // Confirma que a pessoa existe no banco.
                Pessoa? pessoaBanco = conn.Find<Pessoa>(pessoa.Id);

                if (pessoaBanco == null)
                {
                    throw new InvalidOperationException(
                        "A pessoa selecionada não foi encontrada no banco de dados.");
                }

                if (string.IsNullOrWhiteSpace(pessoaBanco.Nome))
                {
                    throw new InvalidOperationException(
                        "O nome da pessoa cadastrada é obrigatório.");
                }

                string cpfBanco = new string(
                    (pessoaBanco.Cpf ?? string.Empty)
                        .Where(char.IsDigit)
                        .ToArray());

                if (cpfBanco.Length != 11)
                {
                    throw new InvalidOperationException(
                        "O CPF da pessoa cadastrada deve conter 11 dígitos.");
                }

                // Impede CPF duplicado em outra pessoa.
                var pessoasBanco = conn.Table<Pessoa>().ToList();

                bool cpfDuplicado = pessoasBanco.Any(p =>
                    p.Id != pessoa.Id &&
                    new string(
                        (p.Cpf ?? string.Empty)
                            .Where(char.IsDigit)
                            .ToArray()) == cpfBanco);

                if (cpfDuplicado)
                {
                    throw new InvalidOperationException(
                        "Já existe outra pessoa cadastrada com este CPF.");
                }

                // O ID do paciente é o mesmo ID da pessoa.
                paciente.Id = pessoaBanco.Id;

                Paciente? pacienteBanco =
                    conn.Find<Paciente>(pessoaBanco.Id);

                if (pacienteBanco == null)
                {
                    conn.Insert(paciente);
                }
                else
                {
                    conn.Update(paciente);
                }
            });

            return paciente.Id;
        }

        public async Task<int> EditarPaciente(
            Paciente paciente,
            Pessoa pessoa)
        {
            if (paciente == null)
                throw new ArgumentNullException(nameof(paciente));

            if (pessoa == null)
                throw new ArgumentNullException(nameof(pessoa));

            if (paciente.Id <= 0 || paciente.Id != pessoa.Id)
            {
                throw new ArgumentException(
                    "O ID do paciente deve ser igual ao ID da pessoa.");
            }

            var existente = await BuscarPaciente(paciente.Id);

            if (existente == null)
            {
                throw new InvalidOperationException(
                    "O paciente não foi encontrado para edição.");
            }

            // SalvarPaciente já faz a atualização quando o ID existe.
            return await SalvarPaciente(paciente, pessoa);
        }

        public async Task ExcluirPacienteComHistorico(int idPaciente)
        {
            if (idPaciente <= 0)
                throw new ArgumentException("ID de paciente inválido.");

            var paciente = await BuscarPaciente(idPaciente);

            if (paciente == null)
                throw new InvalidOperationException(
                    "Paciente não encontrado.");

            // Exclui o histórico antes do paciente.
            var historicos =
                await BuscarHistoricoPaciente(idPaciente);

            foreach (var historico in historicos)
            {
                await ExcluirHistoricoVacinal(historico);
            }

            // Remove somente o registro de Paciente.
            // Não remove Pessoa.
            await ExcluirPaciente(paciente);
        }


        public async Task<List<Paciente>> ConsultarPacientes()
        {
            return await _database
                .Table<Paciente>()
                .ToListAsync();
        }


        public async Task<int> ExcluirPaciente(Paciente paciente)
        {
            return await PacienteHelper.Excluir(paciente);
        }

        // ============================================================
        // LISTAR GENÉRICO
        // ============================================================

        public async Task<List<T>> Listar<T>()
            where T : new()
        {
            return await _database
                .Table<T>()
                .ToListAsync();
        }

        // ============================================================
        // RELATÓRIOS
        // ============================================================

        public async Task<int> SalvarRelatorioVacinalPaciente(
            RelatorioVacinalPaciente relatorio)
        {
            if (relatorio.Id == 0)
                return await _database.InsertAsync(relatorio);

            return await _database.UpdateAsync(relatorio);
        }

        public async Task<int> SalvarRelatorioPacientesRegulares(
            RelatorioPacientesEmSituacaoRegular relatorio)
        {
            if (relatorio.Id == 0)
                return await _database.InsertAsync(relatorio);

            return await _database.UpdateAsync(relatorio);
        }

        public async Task<int> SalvarRelatorioPacientesAtrasados(
            RelatorioPacienteComVacinasAtrasadas relatorio)
        {
            if (relatorio.Id == 0)
                return await _database.InsertAsync(relatorio);

            return await _database.UpdateAsync(relatorio);
        }

        // ============================================================
        // CONDIÇÕES CLÍNICAS
        // ============================================================

        public async Task<List<CondicaoClinica>>
            ListarCondicoesClinicas()
        {
            return await CondicaoClinicaHelper.Listar();
        }

        public async Task<int> SalvarCondicaoClinica(
            CondicaoClinica condicao)
        {
            return await CondicaoClinicaHelper.Salvar(condicao);
        }

        public async Task<CondicaoClinica?>
            BuscarCondicaoClinica(int id)
        {
            return await CondicaoClinicaHelper.BuscarPorId(id);
        }

        public async Task<int> ExcluirCondicaoClinica(
            CondicaoClinica condicao)
        {
            return await CondicaoClinicaHelper.Excluir(condicao);
        }

        public async Task<List<CondicaoClinica>>
            BuscarCondicoesPaciente(int pacienteId)
        {
            return await CondicaoClinicaHelper
                .BuscarPorPaciente(pacienteId);
        }

        // ============================================================
        // VACINAS
        // ============================================================

        public async Task<List<Vacina>> ListarVacinas()
        {
            return await VacinaHelper.Listar();
        }

        public async Task<int> SalvarVacina(Vacina vacina)
        {
            return await VacinaHelper.Salvar(vacina);
        }

        public async Task<Vacina?> BuscarVacina(int id)
        {
            return await VacinaHelper.BuscarPorId(id);
        }

        public async Task<int> ExcluirVacina(Vacina vacina)
        {
            return await VacinaHelper.Excluir(vacina);
        }

        // ============================================================
        // DOSES
        // ============================================================

        public async Task<List<Dose>> ListarDoses()
        {
            return await DoseHelper.Listar();
        }

        public async Task<int> SalvarDose(Dose dose)
        {
            return await DoseHelper.Salvar(dose);
        }

        public async Task<Dose?> BuscarDose(int id)
        {
            return await DoseHelper.BuscarPorId(id);
        }

        public async Task<int> ExcluirDose(Dose dose)
        {
            return await DoseHelper.Excluir(dose);
        }

        // ============================================================
        // HISTÓRICO VACINAL
        // ============================================================

        public async Task<List<HistoricoVacinal>>
            ListarHistoricoVacinal()
        {
            return await _database
                .Table<HistoricoVacinal>()
                .ToListAsync();
        }

        public async Task<List<HistoricoVacinal>>
            BuscarHistoricoPaciente(int pacienteId)
        {
            return await _database
                .Table<HistoricoVacinal>()
                .Where(h => h.PacienteId == pacienteId)
                .ToListAsync();
        }

        // ============================================================
        // SALVAR HISTÓRICO
        // ============================================================

        public async Task<int> SalvarHistoricoVacinal(
            HistoricoVacinal historico)
        {
            if (historico == null)
                return 0;

            // Uma dose com valor zero não é um registro válido.
            if (historico.DosesTomadas <= 0)
            {
                if (historico.Id > 0)
                    return await _database.DeleteAsync(historico);

                return 0;
            }

            if (historico.DataRegistro == default)
            {
                historico.DataRegistro = DateTime.Now;
            }

            if (historico.DataAplicacao == default)
            {
                if (historico.DataUltimaAplicacao != default)
                {
                    historico.DataAplicacao =
                        historico.DataUltimaAplicacao;
                }
                else
                {
                    historico.DataAplicacao =
                        DateTime.Today;
                }
            }

            if (historico.DataUltimaAplicacao == default)
            {
                historico.DataUltimaAplicacao =
                    historico.DataAplicacao;
            }

            // IMPORTANTE:
            // Uma linha do histórico representa uma dose.
            historico.DosesTomadas = 1;

            return await HistoricoVacinalHelper
                .Salvar(historico);
        }

        // ============================================================
        // EXCLUIR HISTÓRICO
        // ============================================================

        public async Task<int> ExcluirHistoricoVacinal(
            HistoricoVacinal historico)
        {
            if (historico == null)
                return 0;

            return await _database.DeleteAsync(historico);
        }

        public async Task<int> ExcluirHistoricoVacinalAsync(
            HistoricoVacinal historico)
        {
            return await ExcluirHistoricoVacinal(historico);
        }

        // ============================================================
        // BUSCAR HISTÓRICO PELO ID
        // ============================================================

        public async Task<HistoricoVacinal?>
            BuscarHistoricoVacinal(int id)
        {
            return await _database
                .Table<HistoricoVacinal>()
                .Where(h => h.Id == id)
                .FirstOrDefaultAsync();
        }

        // ============================================================
        // NOVO:
        // EXCLUIR HISTÓRICO DE UMA VACINA DE UM PACIENTE
        //
        // A busca é feita primeiro no SQLite e a comparação é feita
        // em memória para evitar problemas de tradução do LINQ.
        // ============================================================

        public async Task<int>
            ExcluirHistoricoPorPacienteEVacina(
                int pacienteId,
                string nomeVacina)
        {
            if (pacienteId <= 0 ||
                string.IsNullOrWhiteSpace(nomeVacina))
            {
                return 0;
            }

            var historicos =
                await BuscarHistoricoPaciente(pacienteId);

            string nomeNormalizado =
                NormalizarVacina(nomeVacina);

            var registros =
                historicos
                    .Where(h =>
                        NormalizarVacina(h.NomeVacina) ==
                        nomeNormalizado)
                    .ToList();

            int quantidade = 0;

            foreach (var registro in registros)
            {
                quantidade +=
                    await _database.DeleteAsync(registro);
            }

            return quantidade;
        }

        // ============================================================
        // NORMALIZAR NOME DE VACINA
        // ============================================================

        private static string NormalizarVacina(
            string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return string.Empty;

            string texto =
                nome.Trim()
                    .ToLowerInvariant()
                    .Replace("-", " ")
                    .Replace("_", " ");

            return texto
                .Replace(" ", "");
        }

        // ============================================================
        // LIMPAR REGISTROS COM ZERO DOSE
        // ============================================================

        public async Task LimparHistoricosSemDose()
        {
            try
            {
                await _database.ExecuteAsync(
                    "DELETE FROM HistoricoVacinal " +
                    "WHERE DosesTomadas <= 0");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Erro ao limpar históricos sem dose: {ex.Message}");
            }
        }

        // ============================================================
        // LIMPAR TODO O HISTÓRICO
        // ============================================================

        public async Task LimparHistorico()
        {
            await _database
                .DeleteAllAsync<HistoricoVacinal>();
        }

        // ============================================================
        // REGRAS VACINAIS
        // ============================================================

        public async Task<List<RegraVacinal>>
            ListarRegrasVacinais()
        {
            return await _database
                .Table<RegraVacinal>()
                .ToListAsync();
        }

        public async Task<List<RegraVacinal>>
            ListarRegrasPorTipo(string tipoCalendario)
        {
            return await _database
                .Table<RegraVacinal>()
                .Where(r =>
                    r.TipoCalendario == tipoCalendario)
                .ToListAsync();
        }

        public async Task<List<RegraVacinal>>
            ListarRegrasPorIdade(int idadeEmMeses)
        {
            return await _database
                .Table<RegraVacinal>()
                .Where(r =>
                    r.IdadeMinimaMeses <= idadeEmMeses &&
                    r.IdadeMaximaMeses >= idadeEmMeses)
                .ToListAsync();
        }

        public async Task<int> SalvarRegraVacinal(
            RegraVacinal regra)
        {
            if (regra.Id == 0)
                return await _database.InsertAsync(regra);

            return await _database.UpdateAsync(regra);
        }

        public async Task<int> AtualizarRegraVacinal(
            RegraVacinal regra)
        {
            return await _database.UpdateAsync(regra);
        }

        public async Task<RegraVacinal?>
            BuscarRegraVacinal(int id)
        {
            return await _database
                .Table<RegraVacinal>()
                .Where(r => r.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<int> ExcluirRegraVacinal(
            RegraVacinal regra)
        {
            return await _database.DeleteAsync(regra);
        }

        public async Task RecarregarRegrasVacinais()
        {
            // CarregarRegrasIniciais já substitui as regras e religa
            // o histórico; não apagar a tabela antes.
            await CarregarRegrasIniciais();

            Preferences.Set(
                "AmarV_Calendario_Versao",
                VersaoCalendario);
        }

        // ============================================================
        // CARREGAR REGRAS INICIAIS
        // ============================================================

        public async Task CarregarRegrasIniciais()
        {
            var regras = new List<RegraVacinal>();

            void Adicionar(
                string vacina,
                int dose,
                int total,
                int min,
                int max,
                string grupo,
                string indicacao,
                string contra =
                    "Anafilaxia a componente ou dose anterior.",
                string especial = "",
                string intervalo = "",
                string periodicidade = "",
                int semana = 0,
                string calendario = "SUS",
                string fonte =
                    FonteCalendarioSP,
                string observacao = "")
            {
                regras.Add(
                    new RegraVacinal
                    {
                        NomeVacina = vacina,
                        NumeroDose = dose,
                        TotalDoses = total,

                        DosesNecessariasCrianca = total,
                        DosesNecessariasAdulto = total,

                        IdadeMinimaMeses = min,
                        IdadeMaximaMeses = max,
                        IdadeReferenciaMeses = min,

                        GrupoEtario = grupo,
                        GrupoEspecial = especial,

                        SemanaGestacionalMinima = semana,

                        IntervaloEntreDoses = intervalo,
                        Periodicidade = periodicidade,

                        TipoCalendario = calendario,
                        FonteCalendario = fonte,

                        // Via usada pelo controle operacional do atendimento.
                        // COVID-19 deve ser conferida conforme o produto cadastrado.
                        ViaAdministracao = ObterViaAdministracao(vacina),

                        Indicacao = indicacao,
                        Contraindicacao = contra,

                        Observacao = observacao
                    });
            }

            // Define a via de administração das regras iniciais.
            // A via deve ser revisada quando houver produto/apresentação diferente.
            string ObterViaAdministracao(string nomeVacina)
            {
                string nome = nomeVacina.Trim().ToLowerInvariant();

                if (nome == "bcg") return "ID";
                if (nome == "rotavírus") return "Oral";
                if (nome == "febre amarela" || nome == "scr" ||
                    nome == "varicela" || nome == "dengue") return "SC";

                if (nome == "hepatite b" || nome == "pentavalente" ||
                    nome == "vip" || nome.StartsWith("pneumocócica") ||
                    nome.StartsWith("meningocócica") || nome == "influenza" ||
                    nome == "covid-19" || nome == "hepatite a" ||
                    nome == "dt" || nome == "dtp" || nome == "dtpa" || nome == "hpv" ||
                    nome == "vvsr" || nome.StartsWith("vsr")) return "IM";

                // Não inventar uma via para vacinas não reconhecidas.
                return string.Empty;
            }

            // ========================================================
            // CALENDÁRIO DO SUS — NORMA TÉCNICA DO PROGRAMA DE
            // IMUNIZAÇÃO, ESTADO DE SÃO PAULO, 2025 (CVE/CPAI)
            // Idades em meses completos; "até 4 anos 11 meses 29 dias"
            // equivale ao limite de 59 meses.
            // ========================================================

            // ========================================================
            // CRIANÇA (0 a 9 anos 11 meses 29 dias)
            // ========================================================

            Adicionar(
                "Hepatite B", 1, 1, 0, 1,
                "Criança",
                "Dose ao nascer, preferencialmente nas primeiras 12 horas de vida. " +
                "Quem não recebeu até 30 dias de vida inicia o esquema com a pentavalente.");

            // 7 anos ou mais sem vacinação: 3 doses (0, 1 e 6 meses).
            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Hepatite B",
                    d,
                    3,
                    84,
                    119,
                    "Criança",
                    "Sem vacinação anterior: 3 doses (0, 1 e 6 meses).",
                    intervalo: d == 2 ? "28 dias" : d == 3 ? "60 dias" : "",
                    observacao: d == 3
                        ? "A 3ª dose pode ser aplicada 2 meses após a 2ª, desde que haja ao menos 4 meses desde a 1ª."
                        : "");
            }

            Adicionar(
                "BCG", 1, 1, 0, 59,
                "Criança",
                "Dose única, idealmente na maternidade; indicada prioritariamente até 4 anos 11 meses 29 dias.",
                "Imunodeficiência grave e outras contraindicações específicas devem ser avaliadas.");

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Pentavalente",
                    d,
                    3,
                    d == 1 ? 2 :
                    d == 2 ? 4 : 6,
                    83,
                    "Criança",
                    "Esquema básico aos 2, 4 e 6 meses (idade mínima de 6 semanas). " +
                    "Crianças até 6 anos 11 meses 29 dias sem comprovação completam com a pentavalente.",
                    intervalo: d > 1 ? "28 dias" : "");
            }

            // A partir de 7 anos a pentavalente/DTP é substituída pela dT.
            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "dT",
                    d,
                    3,
                    84, // Idade Mínima: 7 anos completos (84 meses)
                    119, // Idade Máxima da infância (até 9 anos e 11 meses)
                    "Criança",
                    d == 1
                        ? "1ª dose de dT para atualização vacinal (substitui DTP/Penta a partir dos 7 anos)."
                        : $"Dose {d} de dT para atualização do esquema vacinal.",
                    intervalo: d > 1 ? "60 dias" : "");
            }

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "VIP",
                    d,
                    3,
                    d == 1 ? 2 :
                    d == 2 ? 4 : 6,
                    83,
                    "Criança",
                    "Esquema básico aos 2, 4 e 6 meses.",
                    intervalo: d > 1 ? "28 dias" : "");
            }

            Adicionar(
                "VIP", 4, 4, 15, 83,
                "Criança",
                "Reforço aos 15 meses (único reforço do calendário de rotina).",
                intervalo: "6 meses",
                observacao: "Entre 5 e 6 anos, sem reforço comprovado ou só com reforço de VOPb, aplicar um reforço de VIP.");

            // 7 anos ou mais sem comprovação: 3 doses.
            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "VIP",
                    d,
                    3,
                    84,
                    119,
                    "Criança",
                    "Sem comprovação de vacinação: 3 doses (1ª visita, 2 meses e 4 a 6 meses depois).",
                    intervalo: d > 1 ? "28 dias" : "");
            }

            Adicionar(
                "Rotavírus", 1, 2, 2, 11,
                "Criança",
                "1ª dose aos 2 meses (a partir de 1 mês e 15 dias), até 11 meses e 29 dias.");

            Adicionar(
                "Rotavírus", 2, 2, 4, 23,
                "Criança",
                "2ª dose aos 4 meses (mínimo 3 meses e 15 dias), até 23 meses e 29 dias.",
                intervalo: "28 dias");

            // Pneumocócica 10-valente: 2 doses (2 e 4 meses) + reforço aos 12 meses.
            // De 12 meses a menor de 5 anos sem vacinação anterior: dose única.
            Adicionar(
                "Pneumocócica 10-valente", 1, 3, 2, 59,
                "Criança",
                "1ª dose aos 2 meses (idade mínima de 6 semanas). " +
                "De 12 meses a 4 anos 11 meses 29 dias sem vacinação anterior: dose única.");

            Adicionar(
                "Pneumocócica 10-valente", 2, 3, 4, 11,
                "Criança",
                "2ª dose aos 4 meses.",
                intervalo: "28 dias");

            Adicionar(
                "Pneumocócica 10-valente", 3, 3, 12, 59,
                "Criança",
                "Reforço aos 12 meses.",
                intervalo: "60 dias",
                observacao: "Quem iniciou o esquema após 12 meses não precisa de reforço.");

            Adicionar(
                "Meningocócica C", 1, 2, 3, 11,
                "Criança",
                "1ª dose aos 3 meses.");

            Adicionar(
                "Meningocócica C", 2, 2, 5, 11,
                "Criança",
                "2ª dose aos 5 meses (completar até 11 meses e 29 dias).",
                intervalo: "28 dias");

            Adicionar(
                "Meningocócica ACWY", 1, 1, 12, 59,
                "Criança",
                "Reforço aos 12 meses (quem fez o esquema com meningocócica C antes de 12 meses) " +
                "ou dose única de 12 meses a 4 anos 11 meses 29 dias.",
                observacao: "Quem completou esquema e reforço com meningocócica C é considerado vacinado (intervalo mínimo de 60 dias entre doses).");

            Adicionar(
                "Influenza", 1, 1, 6, 71,
                "Criança",
                "Dose anual de 6 meses a menor de 6 anos, preferencialmente no outono.",
                periodicidade: "Anual",
                observacao: "Primovacinação: 2 doses com 4 semanas de intervalo (até 8 anos 11 meses 29 dias); depois, dose anual única.");

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "COVID-19",
                    d,
                    3,
                    d == 1 ? 6 : d == 2 ? 7 : 9,
                    59,
                    "Criança",
                    "Esquema de 2 ou 3 doses conforme o produto; observar volume da dose e o esquema vigente.",
                    intervalo: d > 1 ? "30 dias" : "");
            }

            Adicionar(
                "Febre Amarela", 1, 2, 9, 119,
                "Criança",
                "1ª dose aos 9 meses.",
                observacao: "Para viajantes, observar antecedência mínima de 10 dias.");

            Adicionar(
                "Febre Amarela", 2, 2, 48, 119,
                "Criança",
                "Reforço aos 4 anos; quem recebeu uma dose antes dos 5 anos recebe dose adicional em qualquer idade.",
                intervalo: "28 dias");

            Adicionar(
                "SCR", 1, 2, 12, 119,
                "Criança",
                "1ª dose aos 12 meses.");

            Adicionar(
                "SCR", 2, 2, 15, 119,
                "Criança",
                "2ª dose aos 15 meses, aplicada como tetraviral (SCR + varicela).",
                intervalo: "28 dias",
                observacao: "Sem tetraviral: SCR e varicela simultaneamente; se não for possível, intervalo de 4 semanas entre elas (mínimo de 15 dias, excepcionalmente).");

            Adicionar(
                "Varicela", 1, 2, 15, 83,
                "Criança",
                "1ª dose aos 15 meses, junto com a 2ª dose de SCR (tetraviral).",
                observacao: "A vacina varicela de rotina pode ser aplicada até 6 anos 11 meses 29 dias.");

            Adicionar(
                "Varicela", 2, 2, 48, 83,
                "Criança",
                "2ª dose aos 4 anos.",
                intervalo: "90 dias",
                observacao: "Com atraso vacinal, respeitar o intervalo mínimo de 3 meses entre a 1ª e a 2ª dose.");

            Adicionar(
                "Hepatite A", 1, 1, 15, 59,
                "Criança",
                "Dose única aos 15 meses (até 4 anos 11 meses 29 dias).");

            Adicionar(
                "DTP", 1, 2, 15, 83,
                "Criança",
                "1º reforço aos 15 meses (pode ser aplicado até 6 anos 11 meses 29 dias).");

            Adicionar(
                "DTP", 2, 2, 48, 83,
                "Criança",
                "2º reforço aos 4 anos (limite de 6 anos 11 meses 29 dias).",
                intervalo: "6 meses");

            Adicionar(
                "HPV", 1, 1, 108, 179,
                "Criança",
                "Dose única, de 9 a 14 anos 11 meses 29 dias (preferencialmente aos 9 anos).");

            // Pneumocócica 23-valente: indígenas a partir de 5 anos.
            foreach (var faixa in new[]
            {
                ("Criança", 60, 119),
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719),
                ("Idoso", 720, 9999)
            })
            {
                for (int d = 1; d <= 2; d++)
                {
                    Adicionar(
                        "Pneumocócica 23-valente",
                        d,
                        2,
                        faixa.Item2,
                        faixa.Item3,
                        faixa.Item1,
                        d == 1
                            ? "População indígena a partir de 5 anos sem comprovação de vacinação com pneumocócica conjugada: 1 dose."
                            : "Reforço único, respeitando o intervalo mínimo de 5 anos da dose inicial.",
                        especial: "Indígena",
                        intervalo: d == 2 ? "5 anos" : "");
                }
            }

            // ========================================================
            // ADOLESCENTE / JOVEM
            // ========================================================

            Adicionar(
                "HPV", 1, 1, 120, 179,
                "Adolescente/Jovem",
                "Dose única até 14 anos 11 meses 29 dias, se ainda não vacinado.");

            Adicionar(
                "HPV", 1, 1, 180, 239,
                "Adolescente/Jovem",
                "Adolescentes de 15 a 19 anos que perderam a oportunidade: dose única (resgate).",
                observacao: "Estratégia de resgate em caráter temporário; conferir a vigência na nota técnica específica. " +
                            "Esquemas diferenciados (PrEP, abuso sexual, PRR, NIC 2+, HIV/imunodeprimidos) seguem o CRIE.");

            for (int d = 1; d <= 2; d++)
            {
                Adicionar(
                    "Dengue",
                    d,
                    2,
                    120,
                    179,
                    "Adolescente/Jovem",
                    d == 1
                        ? "1ª dose, de 10 a 14 anos 11 meses 29 dias (vacina dengue atenuada)."
                        : "2ª dose, com 12 semanas de intervalo da 1ª; se atrasar, não reiniciar o esquema.",
                    "Contraindicada em gestantes e lactantes, imunodeficiência (inclusive HIV sintomático ou com comprometimento imunológico), " +
                    "imunossupressores ou corticoide em dose alta nas 4 semanas anteriores e hipersensibilidade a dose anterior.",
                    intervalo: d == 2 ? "84 dias" : "",
                    observacao: "Após dengue confirmada, aguardar 6 meses para iniciar o esquema. Não intercambiar produtos. " +
                                "Vacinas vivas não aplicadas no mesmo dia exigem intervalo de 4 semanas.");
            }

            Adicionar(
                "Meningocócica ACWY", 1, 1, 132, 179,
                "Adolescente/Jovem",
                "Dose única de 11 a 14 anos 11 meses 29 dias, independentemente de vacinação anterior com meningocócica C.");

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Hepatite B",
                    d,
                    3,
                    120,
                    299,
                    "Adolescente/Jovem",
                    "Sem vacinação anterior: 3 doses (0, 1 e 6 meses).",
                    intervalo: d == 2 ? "28 dias" : d == 3 ? "60 dias" : "");
            }

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "VIP",
                    d,
                    3,
                    120,
                    239,
                    "Adolescente/Jovem",
                    "Sem comprovação de vacinação: 3 doses (1ª visita, 2 meses e 4 a 6 meses depois).",
                    intervalo: d > 1 ? "28 dias" : "");
            }

            Adicionar(
                "SCR", 1, 2, 120, 299,
                "Adolescente/Jovem",
                "2 doses conforme histórico.");

            Adicionar(
                "SCR", 2, 2, 120, 299,
                "Adolescente/Jovem",
                "2ª dose conforme histórico.",
                intervalo: "28 dias");

            Adicionar(
                "dT", 1, 3, 120, 299,
                "Adolescente/Jovem",
                "Atualização do esquema conforme histórico.");

            Adicionar(
                "dT", 2, 3, 120, 299,
                "Adolescente/Jovem",
                "Atualização do esquema conforme histórico.",
                intervalo: "60 dias");

            Adicionar(
                "dT", 3, 3, 120, 299,
                "Adolescente/Jovem",
                "Completar o esquema de 3 doses.",
                intervalo: "60 dias",
                periodicidade: "10 anos");

            // ========================================================
            // ADULTO (25 a 59 anos)
            // ========================================================

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Hepatite B",
                    d,
                    3,
                    300,
                    719,
                    "Adulto",
                    "3 doses conforme histórico vacinal.",
                    intervalo: d == 2 ? "28 dias" : d == 3 ? "60 dias" : "");
            }

            Adicionar(
                "dT", 1, 3, 300, 719,
                "Adulto",
                "3 doses conforme histórico vacinal.");

            Adicionar(
                "dT", 2, 3, 300, 719,
                "Adulto",
                "3 doses conforme histórico vacinal.",
                intervalo: "60 dias");

            Adicionar(
                "dT", 3, 3, 300, 719,
                "Adulto",
                "Após esquema completo, reforço a cada 10 anos; antecipar para 5 anos em situação de risco.",
                periodicidade: "10 anos");

            Adicionar(
                "SCR", 1, 2, 300, 359,
                "Adulto",
                "Até 29 anos: 2 doses conforme histórico.");

            Adicionar(
                "SCR", 2, 2, 300, 359,
                "Adulto",
                "Até 29 anos: 2 doses conforme histórico.",
                intervalo: "28 dias");

            Adicionar(
                "SCR", 1, 1, 360, 719,
                "Adulto",
                "De 30 a 59 anos: ao menos 1 dose conforme histórico.");

            Adicionar(
                "SCR", 2, 2, 360, 719,
                "Adulto",
                "Profissional de saúde: 2 doses, com intervalo de 4 semanas.",
                especial: "Profissional de saúde",
                intervalo: "28 dias");

            // ========================================================
            // IDOSO (60 anos ou mais)
            // ========================================================

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Hepatite B",
                    d,
                    3,
                    720,
                    9999,
                    "Idoso",
                    "3 doses conforme histórico vacinal.",
                    intervalo: d == 2 ? "28 dias" : d == 3 ? "60 dias" : "");
            }

            Adicionar(
                "dT", 1, 3, 720, 9999,
                "Idoso",
                "3 doses conforme histórico vacinal.");

            Adicionar(
                "dT", 2, 3, 720, 9999,
                "Idoso",
                "3 doses conforme histórico vacinal.",
                intervalo: "60 dias");

            Adicionar(
                "dT", 3, 3, 720, 9999,
                "Idoso",
                "Após esquema completo, reforço a cada 10 anos; antecipar para 5 anos em situação de risco.",
                periodicidade: "10 anos");

            Adicionar(
                "Influenza", 1, 1, 720, 9999,
                "Idoso",
                "1 dose anual, de preferência no outono.",
                periodicidade: "Anual");

            Adicionar(
                "COVID-19", 1, 1, 720, 9999,
                "Idoso",
                "1 dose a cada 6 meses, independentemente de doses anteriores.",
                periodicidade: "Semestral");

            // Pneumocócica 23-valente: 60 anos ou mais acamados/institucionalizados.
            for (int d = 1; d <= 2; d++)
            {
                Adicionar(
                    "Pneumocócica 23-valente",
                    d,
                    2,
                    720,
                    9999,
                    "Idoso",
                    d == 1
                        ? "Acamado ou institucionalizado, não vacinado: 1 dose."
                        : "Reforço único, respeitando o intervalo mínimo de 5 anos da dose inicial.",
                    especial: "Acamado/Institucionalizado",
                    intervalo: d == 2 ? "5 anos" : "");
            }

            // Febre amarela a partir de 10 anos: dose única; quem recebeu a
            // 1ª dose antes de completar 5 anos precisa de uma dose adicional
            // (o serviço só cobra a 2ª dose nesse caso).
            foreach (var faixaFA in new[]
            {
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719),
                ("Idoso", 720, 9999)
            })
            {
                for (int d = 1; d <= 2; d++)
                {
                    Adicionar(
                        "Febre Amarela",
                        d,
                        2,
                        faixaFA.Item2,
                        faixaFA.Item3,
                        faixaFA.Item1,
                        d == 1
                            ? (faixaFA.Item1 == "Idoso"
                                ? "Dose única, em especial para residentes ou viajantes de áreas com circulação do vírus, sempre após avaliação de risco e de comorbidades."
                                : "Dose única.")
                            : "Dose adicional para quem recebeu a 1ª dose antes dos 5 anos de idade.",
                        intervalo: d == 2 ? "28 dias" : "",
                        observacao: d == 1
                            ? "Viajantes devem observar antecedência mínima de 10 dias."
                            : "");
                }
            }

            // ========================================================
            // GRUPOS ESPECIAIS (norma SP 2025)
            // ========================================================

            // SCR: profissionais de saúde, 2 doses em qualquer idade.
            for (int d = 1; d <= 2; d++)
            {
                Adicionar(
                    "SCR",
                    d,
                    2,
                    720,
                    9999,
                    "Idoso",
                    "Profissional de saúde: 2 doses, com intervalo mínimo de 4 semanas.",
                    especial: "Profissional de saúde",
                    intervalo: d == 2 ? "28 dias" : "",
                    observacao: "A partir de 60 anos, avaliar risco-benefício e contraindicações.");
            }

            // Varicela: profissionais de saúde e população indígena.
            foreach (var especialVaricela in new[] { "Profissional de saúde", "Indígena" })
            {
                foreach (var faixa in new[]
                {
                    ("Criança", 84, 119, "90 dias"),
                    ("Adolescente/Jovem", 120, 299, "60 dias"),
                    ("Adulto", 300, 719, "60 dias"),
                    ("Idoso", 720, 9999, "60 dias")
                })
                {
                    for (int d = 1; d <= 2; d++)
                    {
                        Adicionar(
                            "Varicela",
                            d,
                            2,
                            faixa.Item2,
                            faixa.Item3,
                            faixa.Item1,
                            "Sem comprovação de vacinação: 1 ou 2 doses conforme o laboratório produtor.",
                            especial: especialVaricela,
                            intervalo: d == 2 ? faixa.Item4 : "",
                            observacao: "Menores de 13 anos: intervalo de 3 meses; demais: 8 semanas (excepcionalmente 4).");
                    }
                }
            }

            // dTpa: profissionais e estagiários da saúde e parteiras tradicionais.
            foreach (var faixa in new[]
            {
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719),
                ("Idoso", 720, 9999)
            })
            {
                Adicionar(
                    "dTpa",
                    1,
                    1,
                    faixa.Item2,
                    faixa.Item3,
                    faixa.Item1,
                    "Profissional ou estagiário da saúde e parteira tradicional: 1 dose de dTpa " +
                    "(mínimo de 4 semanas da última dT) e reforço a cada 10 anos.",
                    especial: "Profissional de saúde",
                    periodicidade: "10 anos",
                    observacao: "Com esquema básico incompleto, completar com dT para totalizar 3 doses. " +
                                "A dTpa pode substituir a dose de dT quando ambas estiverem indicadas.");
            }

            // dTpa no puerpério (se não aplicada na gestação).
            foreach (var faixa in new[]
            {
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719)
            })
            {
                Adicionar(
                    "dTpa",
                    1,
                    1,
                    faixa.Item2,
                    faixa.Item3,
                    faixa.Item1,
                    "Puérpera que não recebeu dTpa na gestação: 1 dose o mais precocemente possível, até 45 dias após o parto.",
                    especial: "Puérpera",
                    observacao: "Quem recebeu só 2 doses de vacina com toxoide tetânico na gestação deve completar a 3ª dose com dT após o parto.");
            }

            // Influenza e COVID-19: grupos prioritários (estratégia especial).
            // Inclui puérperas, profissionais de saúde, indígenas, ILPI e
            // pessoas com condições imunológicas/clínicas especiais.
            foreach (var faixa in new[]
            {
                ("Criança", 72, 119),
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719)
            })
            {
                Adicionar(
                    "Influenza",
                    1,
                    1,
                    faixa.Item2,
                    faixa.Item3,
                    faixa.Item1,
                    "Grupo prioritário (estratégia especial): dose anual, de preferência no outono.",
                    especial: "Grupo prioritário",
                    periodicidade: "Anual",
                    observacao: "Inclui puérperas (até 45 dias), doenças crônicas e condições especiais, deficiência permanente, " +
                                "profissionais de saúde, população indígena, quilombola e ribeirinha, situação de rua, " +
                                "professores, forças de segurança, caminhoneiros e transporte coletivo, portuários e sistema prisional. " +
                                "Conferir a estratégia vigente.");
            }

            foreach (var faixa in new[]
            {
                ("Criança", 60, 119),
                ("Adolescente/Jovem", 120, 299),
                ("Adulto", 300, 719)
            })
            {
                Adicionar(
                    "COVID-19",
                    1,
                    1,
                    faixa.Item2,
                    faixa.Item3,
                    faixa.Item1,
                    "Grupo prioritário com 5 anos ou mais: 1 ou 2 doses por ano, conforme produto e nota técnica vigente.",
                    especial: "Grupo prioritário",
                    periodicidade: "Anual",
                    observacao: "Inclui puérperas (até 45 dias), imunocomprometidos, comorbidades, profissionais de saúde, " +
                                "deficiência permanente, ILPI, população indígena, quilombola e ribeirinha, privados de liberdade e situação de rua. " +
                                "Quem desenvolveu miocardite/pericardite após vacina de RNAm não deve receber nova dose sem avaliação médica.");
            }

            // ========================================================
            // GESTANTE
            // ========================================================

            for (int d = 1; d <= 3; d++)
            {
                Adicionar(
                    "Hepatite B",
                    d,
                    3,
                    120,
                    9999,
                    "Gestante",
                    "3 doses conforme histórico vacinal.",
                    intervalo: d == 2 ? "28 dias" : d == 3 ? "60 dias" : "");
            }

            Adicionar(
                "dT", 1, 3, 120, 9999,
                "Gestante",
                "3 doses conforme histórico vacinal.");

            Adicionar(
                "dT", 2, 3, 120, 9999,
                "Gestante",
                "3 doses conforme histórico vacinal.",
                intervalo: "60 dias");

            Adicionar(
                "dT", 3, 3, 120, 9999,
                "Gestante",
                "3 doses conforme histórico vacinal.",
                intervalo: "60 dias");

            Adicionar(
                "Influenza", 1, 1, 120, 9999,
                "Gestante",
                "1 dose em qualquer fase da gestação, a cada gestação.",
                periodicidade: "Anual");

            Adicionar(
                "COVID-19", 1, 1, 120, 9999,
                "Gestante",
                "1 dose em qualquer idade gestacional, a cada gestação.");

            Adicionar(
                "dTpa", 1, 1, 120, 9999,
                "Gestante",
                "1 dose a partir da 20ª semana, a cada gestação.",
                semana: 20,
                observacao: "Se não aplicada na gestação, aplicar no puerpério (até 45 dias).");

            Adicionar(
                "VSR (vírus sincicial respiratório A e B)", 1, 1, 120, 9999,
                "Gestante",
                "Dose única a cada gestação, a partir da 28ª semana, sem restrição de idade materna.",
                semana: 28,
                observacao: "Pode ser aplicada junto com dTpa, influenza, hepatite B e COVID-19. " +
                            "Recém-nascidos prematuros (< 37 semanas) recebem nirsevimabe independentemente da vacinação materna.");

            // ========================================================
            // REDE PRIVADA
            // ========================================================

            Adicionar(
                "Meningocócica B", 1, 2, 0, 9999,
                "Todos",
                "Disponível na rede privada conforme indicação e produto utilizado.",
                calendario: "Rede Privada",
                fonte: "Calendário da Rede Privada");

            Adicionar(
                "Meningocócica ACWY", 1, 1, 0, 9999,
                "Todos",
                "Disponível na rede privada conforme indicação e produto utilizado.",
                calendario: "Rede Privada",
                fonte: "Calendário da Rede Privada");

            Adicionar(
                "Pneumocócica 13", 1, 1, 0, 9999,
                "Todos",
                "Disponível na rede privada conforme indicação e produto utilizado.",
                calendario: "Rede Privada",
                fonte: "Calendário da Rede Privada");

            // ========================================================
            // SALVAR
            // ========================================================

            // As regras ganham novos Ids a cada recarga; guarda as antigas
            // para não perder o vínculo do histórico vacinal dos pacientes.
            var regrasAntigas = await RegraVacinalHelper.Listar();

            await RegraVacinalHelper.Limpar();

            foreach (var regra in regras)
            {
                await RegraVacinalHelper.Salvar(regra);
            }

            await RevincularHistoricoAsync(regrasAntigas, regras);
        }

        // ============================================================
        // CALENDÁRIO: VERSÃO, FONTE E VÍNCULO DO HISTÓRICO
        // ============================================================

        private const string VersaoCalendario = "SP-2026.05";

        private const string FonteCalendarioSP =
            "Norma Técnica do Programa de Imunização - SP 2025 (CVE/CPAI, atualizada em maio/2026; Resolução SS nº 105/2026)";

        public static string ChaveRegra(
            string? nome,
            int dose,
            string? tipo)
        {
            string n = (nome ?? string.Empty).Trim().ToLowerInvariant();

            // "Pneumocócica 20-valente" foi substituída pela 10-valente
            // (norma SP 2025); os registros antigos continuam valendo.
            if (n.StartsWith("pneumocócica 20"))
                n = "pneumocócica 10-valente";

            return $"{n}|{dose}|{(tipo ?? string.Empty).Trim().ToLowerInvariant()}";
        }

        // Histórico recebido do servidor: o servidor guarda os Ids das regras
        // de quando o dado foi enviado. Aqui cada registro é religado à regra
        // equivalente (vacina, dose e calendário) DESTE aparelho; o calendário
        // em si vem sempre da versão do aplicativo.
        public async Task<List<HistoricoVacinal>> RevincularHistoricoRecebidoAsync(
            List<RegraVacinal>? regrasDoServidor,
            List<HistoricoVacinal> historico)
        {
            if (regrasDoServidor == null ||
                regrasDoServidor.Count == 0 ||
                historico.Count == 0)
                return historico;

            var locais = await RegraVacinalHelper.Listar();

            if (locais.Count == 0)
                return historico;

            var servidorPorId = regrasDoServidor
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var locaisPorChave = locais
                .GroupBy(r => ChaveRegra(
                    r.NomeVacina, r.NumeroDose, r.TipoCalendario))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var h in historico)
            {
                if (!servidorPorId.TryGetValue(
                        h.RegraVacinalId, out var daqueleServidor))
                    continue;

                string chave = ChaveRegra(
                    daqueleServidor.NomeVacina,
                    daqueleServidor.NumeroDose,
                    daqueleServidor.TipoCalendario);

                if (!locaisPorChave.TryGetValue(chave, out var candidatas))
                    continue;

                var local =
                    candidatas.FirstOrDefault(r =>
                        string.Equals(
                            r.GrupoEtario,
                            daqueleServidor.GrupoEtario,
                            StringComparison.OrdinalIgnoreCase))
                    ?? candidatas[0];

                h.RegraVacinalId = local.Id;
                h.NomeVacina = local.NomeVacina;
            }

            return historico;
        }

        // Religa cada registro do histórico à nova regra equivalente
        // (mesma vacina, dose e calendário). Registros sem equivalente
        // são mantidos e aparecem como "registros antigos".
        private async Task RevincularHistoricoAsync(
            List<RegraVacinal> antigas,
            List<RegraVacinal> novas)
        {
            if (antigas.Count == 0)
                return;

            var historicos = await _database
                .Table<HistoricoVacinal>()
                .ToListAsync();

            if (historicos.Count == 0)
                return;

            var antigasPorId = antigas
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var novasPorChave = novas
                .GroupBy(r => ChaveRegra(
                    r.NomeVacina, r.NumeroDose, r.TipoCalendario))
                .ToDictionary(g => g.Key, g => g.ToList());

            await _database.RunInTransactionAsync(conn =>
            {
                foreach (var h in historicos)
                {
                    if (!antigasPorId.TryGetValue(
                            h.RegraVacinalId, out var antiga))
                        continue;

                    string chave = ChaveRegra(
                        antiga.NomeVacina,
                        antiga.NumeroDose,
                        antiga.TipoCalendario);

                    if (!novasPorChave.TryGetValue(
                            chave, out var candidatas))
                        continue;

                    var nova =
                        candidatas.FirstOrDefault(r =>
                            string.Equals(
                                r.GrupoEtario,
                                antiga.GrupoEtario,
                                StringComparison.OrdinalIgnoreCase))
                        ?? candidatas[0];

                    bool mudou = h.RegraVacinalId != nova.Id;

                    h.RegraVacinalId = nova.Id;

                    if (!string.Equals(
                            h.NomeVacina,
                            nova.NomeVacina,
                            StringComparison.Ordinal))
                    {
                        h.NomeVacina = nova.NomeVacina;
                        mudou = true;
                    }

                    if (mudou)
                        conn.Update(h);
                }
            });
        }

        // ============================================================
        // ANÁLISE
        // ============================================================

        public async Task<int> SalvarAnaliseVacinal(
            AnaliseEsquemaVacinal analise)
        {
            return await AnaliseEsquemaVacinalHelper
                .Salvar(analise);
        }

        public async Task<List<AnaliseEsquemaVacinal>>
            ListarAnalisesVacinais()
        {
            return await AnaliseEsquemaVacinalHelper
                .Listar();
        }

        public async Task<AnaliseEsquemaVacinal?>
            BuscarAnaliseVacinal(int id)
        {
            return await AnaliseEsquemaVacinalHelper
                .BuscarPorId(id);
        }

        public async Task<List<AnaliseEsquemaVacinal>>
            BuscarAnalisesPaciente(int pacienteId)
        {
            return await AnaliseEsquemaVacinalHelper
                .BuscarPorPaciente(pacienteId);
        }

        public async Task LimparAnalises()
        {
            await AnaliseEsquemaVacinalHelper.Limpar();
        }

        public async Task LimparAnalise()
        {
            await _database
                .DeleteAllAsync<AnaliseEsquemaVacinal>();
        }

        // ============================================================
        // RELATÓRIOS
        // ============================================================

        public async Task<List<Relatorio>>
            ListarRelatorios()
        {
            return await RelatorioHelper.Listar();
        }

        public async Task<int> SalvarRelatorio(
            Relatorio relatorio)
        {
            return await RelatorioHelper.Salvar(relatorio);
        }

        public async Task<List<RelatorioVacinalPaciente>>
            ListarRelatoriosVacinais()
        {
            return await RelatorioHelper.ListarVacinais();
        }

        public async Task<List<RelatorioPacienteComVacinasAtrasadas>>
            ListarRelatoriosPacientesAtrasados()
        {
            return await RelatorioHelper
                .ListarPacientesAtrasados();
        }

        public async Task<List<RelatorioPacientesEmSituacaoRegular>>
            ListarRelatoriosPacientesRegulares()
        {
            return await RelatorioHelper
                .ListarPacientesRegulares();
        }

        // ============================================================
        // SINCRONIZAÇÃO
        // ============================================================

        public async Task<List<Sincronizacao>>
            ListarSincronizacoes()
        {
            return await SincronizacaoHelper.Listar();
        }

        // ============================================================
        // BACKUP
        // ============================================================

        public async Task<List<Backup>>
            ListarBackups()
        {
            return await BackupHelper.Listar();
        }

        public async Task<int> SalvarBackup(
            Backup backup)
        {
            return await BackupHelper.Salvar(backup);
        }

        // ============================================================
        // REMOVER PESSOAS SEM ID VÁLIDO
        //
        // Registros com Id 0 (ou negativo) nunca são válidos: o servidor
        // os recusa e eles quebram o vínculo com Paciente e Usuário.
        // Podem ter ficado de uma recuperação feita com a versão antiga
        // do servidor, que devolvia o ID com outro nome (idPessoa).
        // ============================================================

        public async Task<int> RemoverPessoasSemId()
        {
            return await _database.ExecuteAsync(
                "DELETE FROM Pessoa WHERE Id IS NULL OR Id <= 0");
        }

        // ============================================================
        // SUBSTITUIR DADOS DA SINCRONIZAÇÃO
        // ============================================================

        public async Task SubstituirDados<T>(
            IEnumerable<T>? dados)
            where T : new()
        {
            if (dados == null)
                return;

            var lista = dados.ToList();

            if (lista.Count == 0)
                return;

            foreach (var item in lista)
            {
                try
                {
                    await _database
                        .InsertOrReplaceAsync(item);
                }
                catch
                {
                    try
                    {
                        await _database
                            .UpdateAsync(item);
                    }
                    catch
                    {
                        // Continua com os demais registros.
                    }
                }
           
            
            
            
            
            }
        }
    }
}