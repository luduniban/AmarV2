using SQLite;

namespace maui_amarv.Models
{
    [SQLite.Table("UsuarioDoSistema")]
    public class UsuarioDoSistema
    {
        // Utiliza o mesmo Id da Pessoa correspondente.
        // É a chave primária desta tabela.
        [PrimaryKey]
        public int Id { get; set; }

        // Dados específicos do acesso ao sistema.
        public string Login { get; set; } = string.Empty;

        public string Senha { get; set; } = string.Empty;

        public string Perfil { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool Ativo { get; set; }

        // Obriga a troca da senha no próximo acesso
        // (conta inicial ou senha redefinida por um administrador).
        public bool PrecisaTrocarSenha { get; set; }

        // Compatibilidade com as telas existentes.
        // Esta propriedade não cria uma coluna no SQLite.
        // O nome deve ser carregado da tabela Pessoa pelo helper.
        [Ignore]
        public string Nome { get; set; } = string.Empty;

        // Verifica as credenciais e se o usuário está ativo.
        public virtual bool RealizarLogin(string login, string senha)
        {
            return Login == login &&
                   maui_amarv.Helpers.SenhaHelper.Verificar(senha, Senha) &&
                   Ativo;
        }

        // Altera a senha do objeto em memória.
        // A persistência deve ser feita pelo UsuarioHelper.
        public virtual void AlterarSenha(string novaSenha)
        {
            Senha = maui_amarv.Helpers.SenhaHelper.EstaProtegida(novaSenha)
                ? novaSenha
                : maui_amarv.Helpers.SenhaHelper.Proteger(novaSenha);
        }

        // Retorna o perfil de acesso.
        public virtual string ConsultarPerfil()
        {
            return Perfil;
        }
    }
}