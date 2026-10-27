using SQLite;

namespace maui_amarv.Models
{
    /// <summary>
    /// Registro de uma análise realizada.
    /// As colunas novas têm os mesmos nomes da tabela analise_esquema_vacinal
    /// do MariaDB, então a sincronização já as envia ao servidor.
    /// O SQLite-net acrescenta as colunas novas automaticamente na tabela existente.
    /// </summary>
    public class AnaliseEsquemaVacinal
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int PacienteId { get; set; }

        public string NomePaciente { get; set; } = string.Empty;

        /// <summary>Texto completo da análise (compatível com as versões anteriores).</summary>
        public string Resultado { get; set; } = string.Empty;

        public DateTime DataAnalise { get; set; }

        public string Status { get; set; } = string.Empty;

        // ---- colunas estruturadas (iguais às do servidor) ----

        /// <summary>REGULAR, COM DOSES PARA APLICAR ou EM ATRASO.</summary>
        public string StatusVacinal { get; set; } = string.Empty;

        public string ResultadoAnalise { get; set; } = string.Empty;

        public string VacinasEmAtraso { get; set; } = string.Empty;

        public string VacinasPendentes { get; set; } = string.Empty;

        public string VacinasDisponiveisSUS { get; set; } = string.Empty;

        public string VacinasDisponiveisRedePrivada { get; set; } = string.Empty;

        public string SugestaoRecuperacao { get; set; } = string.Empty;

        public string VacinasContraindicadas { get; set; } = string.Empty;
    }
}
