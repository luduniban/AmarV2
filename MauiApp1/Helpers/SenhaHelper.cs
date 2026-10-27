using System.Security.Cryptography;

namespace maui_amarv.Helpers
{
    // Proteção de senhas com PBKDF2-SHA256 (hash com sal).
    // Formato armazenado: pbkdf2$<iterações>$<salt base64>$<hash base64>
    public static class SenhaHelper
    {
        private const string Prefixo = "pbkdf2";
        private const int Iteracoes = 100_000;
        private const int TamanhoSalt = 16;
        private const int TamanhoHash = 32;

        public const int TamanhoMinimo = 6;

        // Indica se o texto já está no formato de hash desta classe.
        public static bool EstaProtegida(string? valor)
        {
            return TentarLer(
                valor,
                out _,
                out _,
                out _);
        }

        public static string Proteger(string senha)
        {
            byte[] salt =
                RandomNumberGenerator.GetBytes(TamanhoSalt);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                senha,
                salt,
                Iteracoes,
                HashAlgorithmName.SHA256,
                TamanhoHash);

            return string.Join(
                "$",
                Prefixo,
                Iteracoes,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        // Aceita hash e, para migração, senha antiga em texto puro.
        public static bool Verificar(
            string senhaDigitada,
            string? armazenada)
        {
            if (string.IsNullOrEmpty(armazenada))
                return false;

            if (!TentarLer(
                    armazenada,
                    out int iteracoes,
                    out byte[] salt,
                    out byte[] hashEsperado))
            {
                // Legado: senha gravada em texto puro.
                return CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(senhaDigitada),
                    System.Text.Encoding.UTF8.GetBytes(armazenada));
            }

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                senhaDigitada,
                salt,
                iteracoes,
                HashAlgorithmName.SHA256,
                hashEsperado.Length);

            return CryptographicOperations.FixedTimeEquals(
                hash,
                hashEsperado);
        }

        private static bool TentarLer(
            string? valor,
            out int iteracoes,
            out byte[] salt,
            out byte[] hash)
        {
            iteracoes = 0;
            salt = Array.Empty<byte>();
            hash = Array.Empty<byte>();

            if (string.IsNullOrEmpty(valor))
                return false;

            string[] partes = valor.Split('$');

            if (partes.Length != 4 || partes[0] != Prefixo)
                return false;

            if (!int.TryParse(partes[1], out iteracoes) ||
                iteracoes <= 0)
                return false;

            try
            {
                salt = Convert.FromBase64String(partes[2]);
                hash = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            return salt.Length > 0 && hash.Length > 0;
        }
    }
}
