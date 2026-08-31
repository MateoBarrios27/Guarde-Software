using MimeKit;

namespace GuardeSoftwareAPI.Jobs;

public static class EmailTemplateInlineResources
{
    private sealed record InlineResourceDefinition(string ContentId, string RelativePath);

    private static readonly InlineResourceDefinition[] InmobiliariasResources =
    [
        new("guarde-header", Path.Combine("EmailTemplates", "Inmobiliarias", "encabezado_guarde_16_anios.png")),
        new("guarde-instagram", Path.Combine("EmailTemplates", "Inmobiliarias", "icon_instagram_white.png")),
        new("guarde-web", Path.Combine("EmailTemplates", "Inmobiliarias", "icon_web_white.png")),
        new("guarde-whatsapp", Path.Combine("EmailTemplates", "Inmobiliarias", "icon_whatsapp_white.png"))
    ];

    public static void AddReferencedResources(
        BodyBuilder builder,
        string? html,
        string applicationBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(applicationBaseDirectory))
        {
            throw new ArgumentException(
                "Application base directory is required to resolve inline email resources.",
                nameof(applicationBaseDirectory));
        }

        foreach (var resourceDefinition in InmobiliariasResources)
        {
            string cidReference = $"cid:{resourceDefinition.ContentId}";
            if (!html.Contains(cidReference, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string resourcePath = Path.GetFullPath(
                Path.Combine(applicationBaseDirectory, resourceDefinition.RelativePath));

            if (!File.Exists(resourcePath))
            {
                throw new FileNotFoundException(
                    $"The inline email resource '{resourceDefinition.ContentId}' was not found.",
                    resourcePath);
            }

            var linkedResource = builder.LinkedResources.Add(resourcePath);
            linkedResource.ContentId = resourceDefinition.ContentId;
            linkedResource.ContentDisposition = new ContentDisposition("inline");
        }
    }
}
