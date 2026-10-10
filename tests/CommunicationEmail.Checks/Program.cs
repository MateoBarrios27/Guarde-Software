using System.Text.RegularExpressions;
using GuardeSoftwareAPI.Jobs;
using MimeKit;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description); checks++;
}
void Reject(string html, string description)
{
    bool rejected = false;
    try { CommunicationEmailImages.Prepare(new BodyBuilder(), html); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, description);
}
var flyer = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "Logisticas", "flyer_logisticas.png"));
string dataUri = "data:image/png;base64," + Convert.ToBase64String(flyer);
var builder = new BodyBuilder();
string original = $"<p>Hola, logística.</p><p><img src=\"{dataUri}\" width=\"1122\" height=\"1402\" style=\"width:1122px;height:1402px;min-width:1122px;\"></p>";
string prepared = CommunicationEmailImages.Prepare(builder, original);
builder.HtmlBody = prepared;
Check(!prepared.Contains("data:image"), "Quill data URI removed from outgoing HTML");
Check(original.Contains("data:image") && original.Contains("width=\"1122\""), "saved input remains unchanged");
Check(builder.LinkedResources.Count == 1, "one inline MIME image created");
Check(prepared.Contains("cid:" + builder.LinkedResources[0].ContentId), "HTML CID matches MIME Content-ID");
Check(prepared.Contains("width=\"560\"") && prepared.Contains("max-width:100%") && prepared.Contains("height:auto"), "responsive explicit width and proportional height");
Check(!prepared.Contains("height=\"1402\"") && !prepared.Contains("min-width:1122px"), "oversized height and minimum width removed");
Check(prepared.Contains("<!--[if mso]>") && prepared.Contains("max-width:600px"), "fragment wrapped in fluid email table with Outlook fallback");
Check(builder.LinkedResources[0].ContentDisposition?.Disposition == "inline", "resource marked inline, not regular attachment");
var message = new MimeMessage { Subject = "PRUEBA LOCAL — NO ENVIAR", Body = builder.ToMessageBody() };
using (var stream = new MemoryStream())
{
    message.WriteTo(stream); stream.Position = 0;
    var parsed = MimeMessage.Load(stream);
    Check(parsed.Body is MultipartRelated && parsed.HtmlBody!.Contains("cid:"), "serialized MIME round-trip retains related HTML and image");
    using var decoded = new MemoryStream();
    parsed.BodyParts.OfType<MimePart>().Single(part => part.ContentType.MediaType == "image").Content.DecodeTo(decoded);
    Check(decoded.ToArray().SequenceEqual(flyer), "inline PNG bytes unchanged after MIME round-trip");
}
var repeated = new BodyBuilder();
CommunicationEmailImages.Prepare(repeated, $"<img src='{dataUri}'><img src='{dataUri}' alt='Otro uso'>");
Check(repeated.LinkedResources.Count == 1, "repeated image deduplicated within message");
var quotedBuilder = new BodyBuilder();
string quoted = CommunicationEmailImages.Prepare(quotedBuilder, $"<IMG data-src='ignore' SRC='{dataUri}' ALT='A > B' WIDTH='240' />");
Check(quoted.Contains("width=\"240\"") && quoted.Contains("ALT='A > B'") && quoted.Contains("data-src='ignore'"), "attribute boundaries, case and single quotes handled");
Check(CommunicationEmailImages.Prepare(new BodyBuilder(), $"<img src='{dataUri}' width='24' height='24'>").Contains("width=\"24\""), "small explicit icon width preserved");
string remote = CommunicationEmailImages.Prepare(new BodyBuilder(), "<img src='https://example.invalid/no-fetch.png' style='width:320px;color:red;'>");
Check(remote.Contains("width=\"320\"") && remote.Contains("color:red") && remote.Contains("https://example.invalid"), "remote source preserved without network access, CSS width respected");
Check(CommunicationEmailImages.Prepare(new BodyBuilder(), "<p>Sólo texto.</p>") == "<p>Sólo texto.</p>", "text-only communications unchanged");
Reject("<img src='data:image/png;base64,%%%'>", "invalid base64 rejected clearly");
Reject("<img src='data:image/png;base64,SG9sYQ=='>", "false image signature rejected");
Reject("<img src='data:image/svg+xml;base64,PHN2Zy8+'>", "unsupported SVG rejected");
Reject("<img src='file:///C:/secret.png'>", "local file URL rejected without reading files");
Reject("<img src='blob:https://example.com/123'>", "browser-only blob source rejected");
Reject("<img src='/assets/image.png'>", "relative source rejected rather than sent broken");
Reject("<img src='cid:missing-resource'>", "unresolved inline reference rejected");
Reject(string.Concat(Enumerable.Repeat("<img src='https://example.invalid/image.png'>", 101)), "image count limit enforced");
Reject("<img src='data:image/png;base64," + new string('A', 15 * 1024 * 1024) + "'>", "oversized embedded image rejected before decoding");
var logistics = new BodyBuilder();
string template = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "logisticas.html"));
EmailTemplateInlineResources.AddReferencedResources(logistics, template, AppContext.BaseDirectory);
string logisticsHtml = CommunicationEmailImages.Prepare(logistics, template);
Check(logistics.LinkedResources.Count == 2 && logisticsHtml.Contains("cid:guarde-logisticas-flyer") && logisticsHtml.Contains("cid:guarde-whatsapp"), "logistics template resolves inline flyer and WhatsApp icon");
Check(logisticsHtml.Contains("width=\"560\"") && logisticsHtml.Contains("https://wa.me/5491157800251"), "template keeps constrained flyer and clickable CTA");
Check(logisticsHtml.Contains("Abrir chat en WhatsApp") && logisticsHtml.Contains("width=\"24\""), "WhatsApp CTA keeps its label and small inline logo");
Check(!logisticsHtml.Contains("href=\"tel:") && !logisticsHtml.Contains("www.guardeloquequiera.net") && !logisticsHtml.Contains("www.instagram.com") && !logisticsHtml.Contains("Si preferís no recibir"), "extra contact links and footer copy removed as requested");
Check(!logisticsHtml.Contains("insumos médicos"), "logistics copy stays separate from laboratories");
var existing = new BodyBuilder();
string existingTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "inmobiliarias.html"));
EmailTemplateInlineResources.AddReferencedResources(existing, existingTemplate, AppContext.BaseDirectory);
string existingHtml = CommunicationEmailImages.Prepare(existing, existingTemplate);
Check(existing.LinkedResources.Count == 4 && existingHtml.Contains("width=\"640\"") && existingHtml.Contains("width=\"24\""), "existing inmobiliarias header and icons preserved");
string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SendCommunicationJob.source.txt"));
Check(source.Contains("builder.HtmlBody = CommunicationEmailImages.Prepare(builder, builder.HtmlBody)"), "shared outgoing message path invokes inline conversion");
Check(Regex.Matches(source, @"message = CreateEmailMessage\(").Count >= 2 && source.Contains("if (isTestMode)"), "normal and test flows reach shared MIME builder (source check)");
Console.WriteLine($"ALL {checks} EMAIL CHECKS PASSED — no SMTP, no DB, no sends.");
