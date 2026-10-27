using AmarV.Core.Analise;
using AmarV.Core.Entrada;
using AmarV.Core.Relatorios;
using AmarV.Core.Resultado;
using maui_amarv.Data;
using maui_amarv.Models;
using RegistroAnalise = maui_amarv.Models.AnaliseEsquemaVacinal;

namespace maui_amarv.Services
{
    // =====================================================================
    // SERVIÇO DE ANÁLISE VACINAL (RF08 a RF14)
    //
    // Esta classe agora é só a "ponte" entre o aplicativo e o motor
    // AmarV.Core (projeto separado, testado fora do celular):
    //   1. lê do SQLite o paciente, a pessoa, o histórico e as regras;
    //   2. converte para o formato do motor;
    //   3. devolve o resultado estruturado (ResultadoAnalise);
    //   4. grava a análise com as colunas preenchidas (atrasadas,
    //      pendentes, recuperação, contraindicações...).
    //
    // O motor antigo (≈3.000 linhas) foi guardado em
    // docs/legado/AnaliseEsquemaVacinal_motor_antigo.cs.txt para consulta.
    // =====================================================================
    public class AnaliseEsquemaVacinal
    {
        private readonly BancoDados _bancoDados;

        public AnaliseEsquemaVacinal(BancoDados bancoDados)
        {
            _bancoDados = bancoDados;
        }

        /// <summary>
        /// Parâmetros operacionais (tolerância de atraso, limites por atendimento...).
        /// Altere aqui para mudar o comportamento em todo o aplicativo.
        /// </summary>
        public static ParametrosAnalise CriarParametros() => new()
        {
            DataReferencia = DateTime.Today,
            ToleranciaAtrasoDias = 30,
            HorizonteMeses = 12,
            AplicarLimitesPorAtendimento = true,
            LimiteSubcutaneas = 2,
            LimiteIntramuscularesAte24Meses = 2,
            LimiteIntramuscularesAcima24Meses = 4
        };

        // -----------------------------------------------------------------
        // ANÁLISE ESTRUTURADA (usada pela tela nova e pelos relatórios)
        // -----------------------------------------------------------------

        public async Task<ResultadoAnalise> AnalisarAsync(
            Paciente paciente,
            List<HistoricoVacinal>? historico = null,
            List<RegraVacinal>? regras = null,
            Pessoa? pessoa = null)
        {
            if (paciente == null)
                throw new ArgumentNullException(nameof(paciente));

            regras ??= await _bancoDados.ListarRegrasVacinais();
            historico ??= await _bancoDados.BuscarHistoricoPaciente(paciente.Id);

            if (pessoa == null)
            {
                var pessoas = await _bancoDados.Listar<Pessoa>();
                pessoa = pessoas.FirstOrDefault(p => p.Id == paciente.Id);
            }

            var motor = new MotorAnaliseVacinal(regras.Select(Converter));

            return motor.Analisar(
                Converter(paciente, pessoa),
                historico.Where(h => h.PacienteId == paciente.Id).Select(Converter),
                CriarParametros());
        }

        /// <summary>Analisa todos os pacientes de uma vez (relatórios administrativos).</summary>
        public async Task<List<PacienteRelatorio>> AnalisarTodosAsync(IEnumerable<Paciente>? pacientes = null)
        {
            var listaPacientes = pacientes?.ToList() ?? await _bancoDados.Listar<Paciente>();
            var pessoas = await _bancoDados.Listar<Pessoa>();
            var regras = await _bancoDados.ListarRegrasVacinais();
            var historicos = await _bancoDados.Listar<HistoricoVacinal>();

            var motor = new MotorAnaliseVacinal(regras.Select(Converter));
            var prm = CriarParametros();
            var resultado = new List<PacienteRelatorio>();

            foreach (var paciente in listaPacientes)
            {
                var pessoa = pessoas.FirstOrDefault(p => p.Id == paciente.Id);
                var analise = motor.Analisar(
                    Converter(paciente, pessoa),
                    historicos.Where(h => h.PacienteId == paciente.Id).Select(Converter),
                    prm);

                resultado.Add(new PacienteRelatorio
                {
                    Analise = analise,
                    NomeMae = pessoa?.Nome_da_Mae ?? string.Empty,
                    Telefone = pessoa?.Telefone ?? string.Empty,
                    Endereco = pessoa?.Endereco ?? string.Empty
                });
            }

            return resultado;
        }

        // -----------------------------------------------------------------
        // COMPATIBILIDADE: telas antigas que esperam texto
        // -----------------------------------------------------------------

        public async Task<string> AnalisarPacienteAsync(
            Paciente paciente,
            List<HistoricoVacinal>? historico = null)
        {
            if (paciente == null)
                return "Paciente não informado.";

            var resultado = await AnalisarAsync(paciente, historico);
            return FormatadorTexto.Gerar(resultado);
        }

        // -----------------------------------------------------------------
        // GRAVAR A ANÁLISE (com as colunas que o servidor já possui)
        // -----------------------------------------------------------------

        public async Task<RegistroAnalise> SalvarAsync(ResultadoAnalise resultado)
        {
            var resumo = ResumoParaBanco.De(resultado);

            var registro = new RegistroAnalise
            {
                PacienteId = resultado.PacienteId,
                NomePaciente = resultado.NomePaciente,
                DataAnalise = DateTime.Now,
                Status = "Concluída",
                Resultado = resumo.ResultadoTexto,
                ResultadoAnalise = resumo.ResultadoTexto,
                StatusVacinal = resumo.StatusVacinal,
                VacinasEmAtraso = resumo.VacinasEmAtraso,
                VacinasPendentes = resumo.VacinasPendentes,
                VacinasDisponiveisSUS = resumo.VacinasDisponiveisSUS,
                VacinasDisponiveisRedePrivada = resumo.VacinasDisponiveisRedePrivada,
                SugestaoRecuperacao = resumo.SugestaoRecuperacao,
                VacinasContraindicadas = resumo.VacinasContraindicadas
            };

            await _bancoDados.SalvarAnaliseVacinal(registro);
            return registro;
        }

        // -----------------------------------------------------------------
        // CONVERSÕES (modelos do SQLite -> motor)
        // -----------------------------------------------------------------

        public static PerfilPaciente Converter(Paciente p, Pessoa? pessoa)
        {
            DateTime nascimento = p.DataNascimento;
            if ((nascimento == default || nascimento.Year < 1900) && pessoa != null)
                nascimento = pessoa.DataNascimento;

            return new PerfilPaciente
            {
                Id = p.Id,
                Nome = string.IsNullOrWhiteSpace(pessoa?.Nome) ? $"Paciente {p.Id}" : pessoa!.Nome.Trim(),
                DataNascimento = nascimento,
                Sexo = p.Sexo ?? string.Empty,
                Gestante = p.Gestante,
                SemanasGestacao = p.SemanasGestacao,
                Puerpera = p.Puerpera,
                Lactante = p.Lactante,
                CriancaAmamentadaMenor6Meses = p.CriancaAmamentadaMenor6Meses,
                ProfissionalSaude = p.ProfissionalSaude,
                Indigena = p.Indigena,
                AcamadoOuInstitucionalizado = p.AcamadoOuInstitucionalizado,
                Imunossuprimido = p.Imunossuprimido,
                HIV = p.HIV,
                Quimioterapia = p.Quimioterapia,
                Transplantado = p.Transplantado,
                UsoCorticoideOuImunobiologico = p.UsoCorticoideOuImunobiologico,
                AlergiaGrave = p.AlergiaGrave,
                FebreOuDoencaAguda = p.FebreOuDoencaAguda,
                CondicaoClinica = p.CondicaoClinica ?? string.Empty
            };
        }

        public static DoseAplicada Converter(HistoricoVacinal h)
        {
            // Em registros antigos com várias doses na mesma linha, a data
            // conhecida é a da última aplicação.
            DateTime data = h.DataUltimaAplicacao != default ? h.DataUltimaAplicacao : h.DataAplicacao;

            return new DoseAplicada
            {
                Id = h.Id,
                NomeVacina = h.NomeVacina ?? string.Empty,
                RegraVacinalId = h.RegraVacinalId,
                Quantidade = Math.Max(0, h.DosesTomadas),
                Data = data == default ? null : data,
                Lote = h.Lote ?? string.Empty,
                LocalAplicacao = h.LocalAplicacao ?? string.Empty,
                Profissional = h.ProfissionalResponsavel ?? string.Empty
            };
        }

        public static RegraCalendario Converter(RegraVacinal r) => new()
        {
            Id = r.Id,
            NomeVacina = r.NomeVacina ?? string.Empty,
            NumeroDose = r.NumeroDose,
            TotalDoses = r.TotalDoses,
            IdadeMinimaMeses = r.IdadeMinimaMeses,
            IdadeMaximaMeses = r.IdadeMaximaMeses,
            IdadeReferenciaMeses = r.IdadeReferenciaMeses,
            GrupoEtario = r.GrupoEtario ?? string.Empty,
            GrupoEspecial = r.GrupoEspecial ?? string.Empty,
            SemanaGestacionalMinima = r.SemanaGestacionalMinima,
            IntervaloEntreDoses = r.IntervaloEntreDoses ?? string.Empty,
            Periodicidade = r.Periodicidade ?? string.Empty,
            ViaAdministracao = r.ViaAdministracao ?? string.Empty,
            LocalAplicacao = r.LocalAplicacao ?? string.Empty,
            TipoCalendario = r.TipoCalendario ?? string.Empty,
            FonteCalendario = r.FonteCalendario ?? string.Empty,
            Indicacao = r.Indicacao ?? string.Empty,
            Contraindicacao = r.Contraindicacao ?? string.Empty,
            EventosAdversos = r.EventosAdversos ?? string.Empty,
            Observacao = r.Observacao ?? string.Empty
        };
    }
}
