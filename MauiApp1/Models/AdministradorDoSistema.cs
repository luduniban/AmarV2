using SQLite;

namespace maui_amarv.Models
{
    [SQLite.Table("AdministradorDoSistema")]
    public class AdministradorDoSistema
    {
        // Utiliza o mesmo Id do UsuarioDoSistema correspondente.
        // Chave primária da tabela e identificador do administrador.
        [PrimaryKey]
        public int Id { get; set; }

        // Dados específicos do administrador.
        public string NivelAcesso { get; set; } = string.Empty;

        public void GeUsuarios()
        {
        }

        public void AtualizarVacinasEDoses()
        {
        }

        public void AtualizarCalendarioSUS()
        {
        }

        public void AtualizarCalendarioRedePrivada()
        {
        }

        public void RealizarBackup()
        {
        }
    }
}
