
using SQLite;

namespace maui_amarv.Models
{
    [SQLite.Table("Paciente")]
    public class Paciente
    {
        // Mesmo Id da Pessoa correspondente.
        // Chave primária da tabela Paciente.
        [PrimaryKey]
        public int Id { get; set; }

        // Dados específicos do paciente.
        public DateTime DataNascimento { get; set; }

        public string Sexo { get; set; } = string.Empty;

        public string CartaoSUS { get; set; } = string.Empty;

        public string CondicaoClinica { get; set; } = string.Empty;

        public bool Gestante { get; set; }

        public int SemanasGestacao { get; set; }

        public bool Lactante { get; set; }

        // Puérpera (até 45 dias após o parto).
        public bool Puerpera { get; set; }

        public bool ProfissionalSaude { get; set; }

        public bool Indigena { get; set; }

        public bool AcamadoOuInstitucionalizado { get; set; }

        public bool CriancaAmamentadaMenor6Meses { get; set; }

        public bool Imunossuprimido { get; set; }

        public bool HIV { get; set; }

        public bool Quimioterapia { get; set; }

        public bool Transplantado { get; set; }

        public bool UsoCorticoideOuImunobiologico { get; set; }

        public bool AlergiaGrave { get; set; }

        public bool FebreOuDoencaAguda { get; set; }

        // Propriedades calculadas: não criam colunas no SQLite.
        // A idade é SEMPRE calculada a partir da data de nascimento,
        // para nunca ficar defasada com o passar do tempo.
        [Ignore]
        public int IdadeTotalMeses
        {
            get
            {
                if (DataNascimento == default)
                    return 0;

                DateTime hoje = DateTime.Today;

                int meses =
                    (hoje.Year - DataNascimento.Year) * 12 +
                    hoje.Month - DataNascimento.Month;

                if (hoje.Day < DataNascimento.Day)
                    meses--;

                return Math.Max(0, meses);
            }
        }

        [Ignore]
        public int IdadeAnos => IdadeTotalMeses / 12;

        [Ignore]
        public int IdadeMesesAdicionais => IdadeTotalMeses % 12;

        [Ignore]
        public string IdadeFormatada =>
            $"{IdadeAnos} ano(s) e {IdadeMesesAdicionais} mês(es)";

        // Operações do paciente.
        public void CadastrarPaciente()
        {
        }

        public void AtualizarPaciente()
        {
        }

        public Paciente ConsultarPaciente()
        {
            return this;
        }
    }
}
