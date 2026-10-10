using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MimeKit;

namespace GuardeSoftwareAPI.Jobs;

/// <summary>Converts Quill data-URI images only in the outgoing message; never edits saved content.</summary>
public static class CommunicationEmailImages
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private const int MaxTotalImageBytes = 20 * 1024 * 1024;
    private const int MaxImages = 100;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly Regex ImageTags = new(
        "<img\\b(?:[^\"'>]|\"[^\"]*\"|'[^']*')*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex Attributes = new(
        "(?<=\\s)(?<name>[\\w:-]+)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex DataImage = new(
        @"^data:(?<mime>image/(?:png|jpeg|gif|webp));base64,(?<data>[\s\S]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    public static string Prepare(BodyBuilder builder, string? html)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(html)) return html ?? string.Empty;

        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalBytes = 0;
        int count = 0;
        bool isDocument = Regex.IsMatch(html, @"<html(?:\s|>)", RegexOptions.IgnoreCase, RegexTimeout);
        string prepared = ImageTags.Replace(html, match =>
        {
            if (++count > MaxImages)
                throw new InvalidOperationException("El comunicado supera el límite de 100 imágenes.");

            string tag = match.Value;
            string source = GetAttribute(tag, "src");
            int? naturalWidth = null;
            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var data = DataImage.Match(source);
                if (!data.Success)
                    throw new InvalidOperationException("Usá imágenes PNG, JPEG, GIF o WebP para el correo.");
                string encoded = data.Groups["data"].Value;
                if (encoded.Length > (MaxImageBytes * 4L / 3L) + 8192)
                    throw new InvalidOperationException("Cada imagen del comunicado debe pesar como máximo 10 MB.");
                byte[] bytes;
                try { bytes = Convert.FromBase64String(encoded); }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException("Una imagen del comunicado tiene datos inválidos. Volvé a insertarla.", exception);
                }
                if (bytes.Length == 0 || bytes.Length > MaxImageBytes)
                    throw new InvalidOperationException("Cada imagen del comunicado debe pesar entre 1 byte y 10 MB.");

                string mime = data.Groups["mime"].Value.ToLowerInvariant();
                ValidateSignature(bytes, mime);
                naturalWidth = ReadNaturalWidth(bytes, mime);
                string key = mime + ":" + Convert.ToHexString(SHA256.HashData(bytes));
                if (!resources.TryGetValue(key, out string? contentId))
                {
                    totalBytes += bytes.Length;
                    if (totalBytes > MaxTotalImageBytes)
                        throw new InvalidOperationException("Las imágenes del comunicado superan los 20 MB en total.");
                    contentId = "quill-" + Guid.NewGuid().ToString("N") + "@guardeloquequiera";
                    string extension = mime == "image/jpeg" ? "jpg" : mime[6..];
                    var resource = (MimePart)builder.LinkedResources.Add(
                        $"imagen-{resources.Count + 1}.{extension}", bytes, ContentType.Parse(mime));
                    resource.ContentId = contentId;
                    resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                    resource.ContentTransferEncoding = ContentEncoding.Base64;
                    resources.Add(key, contentId);
                }
                tag = SetAttribute(tag, "src", "cid:" + contentId);
            }
            else if (source.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
            {
                if (!builder.LinkedResources.Any(resource =>
                    string.Equals(resource.ContentId, source[4..], StringComparison.Ordinal)))
                    throw new InvalidOperationException("Falta una imagen inline de la plantilla. Revisá los recursos publicados del backend.");
            }
            else if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                // Do not read arbitrary files or fetch operator-supplied URLs on the server.
                throw new InvalidOperationException("La imagen debe insertarse desde Quill o usar una dirección web pública completa.");
            }

            int maxWidth = isDocument ? 640 : 560;
            int width = ReadRequestedWidth(tag) ?? naturalWidth ?? 560;
            width = Math.Clamp(width, 1, maxWidth);
            tag = SetAttribute(tag, "width", width.ToString(CultureInfo.InvariantCulture));
            tag = RemoveAttribute(tag, "height");
            string style = string.Join(";", GetAttribute(tag, "style").Split(';')
                .Where(declaration => !IsDimensionDeclaration(declaration)).Where(declaration => !string.IsNullOrWhiteSpace(declaration)));
            tag = SetAttribute(tag, "style", style + $";display:block;width:{width}px;max-width:100%;height:auto;border:0;");
            if (string.IsNullOrWhiteSpace(GetAttribute(tag, "alt")))
                tag = SetAttribute(tag, "alt", "Imagen de Guarde Lo Que Quiera");
            return tag;
        });

        if (isDocument || count == 0) return prepared;
        // Width attribute for classic Outlook, fluid table for mobile clients.
        return "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>"
            + "<body style=\"margin:0;padding:0;font-family:Arial,Helvetica,sans-serif;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td align=\"center\" style=\"padding:20px 12px;\">"
            + "<!--[if mso]><table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\"><tr><td><![endif]-->"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;max-width:600px;\"><tr><td style=\"padding:20px;font-size:16px;line-height:1.55;\">"
            + prepared + "</td></tr></table><!--[if mso]></td></tr></table><![endif]--></td></tr></table></body></html>";
    }

    private static string GetAttribute(string tag, string name) => WebUtility.HtmlDecode(
        Attributes.Matches(tag).Cast<Match>().FirstOrDefault(match =>
            string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))?.Groups["value"].Value ?? string.Empty);

    private static string RemoveAttribute(string tag, string name) => Attributes.Replace(tag, match =>
        string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase) ? string.Empty : match.Value);

    private static string SetAttribute(string tag, string name, string value)
    {
        string clean = RemoveAttribute(tag, name);
        int index = clean.LastIndexOf('>');
        if (index > 0 && clean[index - 1] == '/') index--;
        return clean.Insert(index, $" {name}=\"{WebUtility.HtmlEncode(value)}\"");
    }

    private static int? ReadRequestedWidth(string tag)
    {
        if (int.TryParse(GetAttribute(tag, "width"), out int width) && width > 0) return width;
        var match = Regex.Match(GetAttribute(tag, "style"), @"(?:^|;)\s*width\s*:\s*(\d+)px\s*(?:;|$)", RegexOptions.IgnoreCase, RegexTimeout);
        return match.Success && int.TryParse(match.Groups[1].Value, out width) && width > 0 ? width : null;
    }

    private static bool IsDimensionDeclaration(string declaration)
    {
        string property = declaration.Split(':')[0].Trim().ToLowerInvariant();
        return property is "width" or "height" or "max-width" or "min-width" or "max-height" or "min-height" or "display" or "border";
    }

    private static void ValidateSignature(byte[] bytes, string mime)
    {
        bool valid = mime switch
        {
            "image/png" => bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            "image/gif" => bytes.Length >= 10 && (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a"),
            "image/webp" => bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            _ => false
        };
        if (!valid) throw new InvalidOperationException("El formato de una imagen no coincide con sus datos. Volvé a insertarla.");
    }

    private static int? ReadNaturalWidth(byte[] bytes, string mime)
    {
        if (mime == "image/png") return (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)), int.MaxValue);
        if (mime == "image/gif") return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
        return null;
    }
}
