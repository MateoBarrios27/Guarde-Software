using System.Security.Cryptography;
using System.Text;

namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Credencial del visitante. Es un valor aleatorio de 256 bits que sólo conoce
    /// su navegador; la base guarda su hash SHA-256, así que una filtración de la
    /// tabla no permite leer ni escribir conversaciones ajenas.
    /// </summary>
    public static class ChatVisitorToken
    {
        public const int MaxLength = 128;

        public static string Create()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        public static bool Matches(byte[] expectedHash, string? token)
        {
            if (string.IsNullOrEmpty(token) || token.Length > MaxLength || expectedHash.Length != 32)
                return false;

            return CryptographicOperations.FixedTimeEquals(expectedHash, Hash(token));
        }
    }
}
