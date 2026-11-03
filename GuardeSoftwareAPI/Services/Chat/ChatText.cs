using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Saneado de lo que escribe un visitante anónimo. El cliente de Angular y el widget
    /// muestran el texto como texto (nunca como HTML), pero igual se descartan los
    /// caracteres de control y los que invierten el sentido de lectura, que sirven para
    /// disfrazar un mensaje en la bandeja.
    /// </summary>
    public static class ChatText
    {
        public static string Clean(string? value, bool allowNewLines)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            var builder = new StringBuilder(normalized.Length);
            int consecutiveNewLines = 0;

            foreach (char character in normalized)
            {
                if (character == '\n')
                {
                    if (!allowNewLines)
                    {
                        builder.Append(' ');
                    }
                    else if (++consecutiveNewLines <= 2)
                    {
                        builder.Append('\n');
                    }
                    continue;
                }

                consecutiveNewLines = 0;
                if (character == '\t')
                {
                    builder.Append(' ');
                    continue;
                }

                if (char.IsControl(character) || IsInvisibleDirectionControl(character))
                    continue;

                builder.Append(character);
            }

            string cleaned = builder.ToString();
            if (!allowNewLines)
                cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }

        public static string? NormalizeEmail(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string value = raw.Trim().ToLowerInvariant();
            if (value.Length > 254 || value.Any(char.IsWhiteSpace))
                throw new ChatValidationException("El email no es válido.");

            try
            {
                var address = new MailAddress(value);
                if (!string.Equals(address.Address, value, StringComparison.Ordinal) || !address.Host.Contains('.'))
                    throw new ChatValidationException("El email no es válido.");
            }
            catch (FormatException)
            {
                throw new ChatValidationException("El email no es válido.");
            }

            return value;
        }

        public static string? NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string value = Clean(raw, allowNewLines: false);
            int digits = value.Count(char.IsDigit);
            bool onlyPhoneCharacters = value.All(c => char.IsDigit(c) || c is ' ' or '+' or '-' or '(' or ')' or '.');
            if (!onlyPhoneCharacters || digits is < 6 or > 15 || value.Length > 40)
                throw new ChatValidationException("El teléfono no es válido.");

            return value;
        }

        /// <summary>Sólo se guarda una dirección http(s) absoluta: es la página desde donde escribió.</summary>
        public static string? NormalizeSourceUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 500) return null;

            return Uri.TryCreate(raw.Trim(), UriKind.Absolute, out Uri? uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.ToString()
                : null;
        }

        private static bool IsInvisibleDirectionControl(char character)
            => character is >= '‪' and <= '‮'
                or >= '⁦' and <= '⁩'
                or '‎' or '‏' or '﻿';
    }
}
