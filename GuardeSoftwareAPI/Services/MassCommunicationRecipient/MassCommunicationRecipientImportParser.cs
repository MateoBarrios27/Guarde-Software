using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace GuardeSoftwareAPI.Services.massCommunicationRecipient;

public sealed class MassCommunicationRecipientImportRecord
{
    public int RowNumber { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string EmailKey { get; init; } = string.Empty;
}

/// <summary>
/// Reads the simple tabular formats used by the external-recipient agenda.
/// The application does not need to persist the source file: only normalized
/// rows are passed to the transactional DAO.
/// </summary>
public sealed class MassCommunicationRecipientImportParser
{
    private const long MaxFileLength = 20 * 1024 * 1024;
    private const int MaxRows = 10_000;

    public async Task<IReadOnlyList<MassCommunicationRecipientImportRecord>> ParseAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("El archivo de receptores está vacío.");
        }

        if (file.Length > MaxFileLength)
        {
            throw new ArgumentException("El archivo no puede superar los 20 MB.");
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".tsv" and not ".xlsx")
        {
            throw new ArgumentException("El archivo debe ser CSV, TSV o XLSX.");
        }

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer);
        buffer.Position = 0;

        List<string[]> rows = extension == ".xlsx"
            ? ParseXlsx(buffer)
            : ParseDelimited(buffer, extension == ".tsv" ? '\t' : null);

        return MapRows(rows);
    }

    private static List<string[]> ParseDelimited(Stream stream, char? forcedDelimiter)
    {
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true);

        string text = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text)) return [];

        char delimiter = forcedDelimiter ?? DetectDelimiter(text);
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];

            if (current == '"')
            {
                if (insideQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (!insideQuotes && current == delimiter)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            if (!insideQuotes && (current == '\r' || current == '\n'))
            {
                if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(currentField.ToString());
                currentField.Clear();
                AddRowIfNotEmpty(rows, currentRow);
                currentRow = [];
                continue;
            }

            currentField.Append(current);
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            AddRowIfNotEmpty(rows, currentRow);
        }

        return rows;
    }

    private static void AddRowIfNotEmpty(List<string[]> rows, List<string> row)
    {
        if (row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
        {
            rows.Add(row.ToArray());
        }
    }

    private static char DetectDelimiter(string text)
    {
        string firstLine = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        return new[] { ';', ',', '\t' }
            .OrderByDescending(delimiter => CountOutsideQuotes(firstLine, delimiter))
            .FirstOrDefault(';');
    }

    private static int CountOutsideQuotes(string text, char delimiter)
    {
        bool insideQuotes = false;
        int count = 0;
        foreach (char current in text)
        {
            if (current == '"') insideQuotes = !insideQuotes;
            else if (!insideQuotes && current == delimiter) count++;
        }

        return count;
    }

    private static List<string[]> ParseXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var sharedStrings = ReadSharedStrings(archive);
        ZipArchiveEntry worksheet = FindFirstWorksheet(archive);
        XDocument document;

        using (Stream worksheetStream = worksheet.Open())
        {
            document = XDocument.Load(worksheetStream);
        }

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]>();

        foreach (XElement rowElement in document.Descendants(main + "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (XElement cell in rowElement.Elements(main + "c"))
            {
                string? reference = cell.Attribute("r")?.Value;
                if (string.IsNullOrWhiteSpace(reference)) continue;

                int columnIndex = GetColumnIndex(reference);
                string value = ReadCellValue(cell, main, sharedStrings);
                cells[columnIndex] = value;
            }

            if (cells.Count == 0) continue;

            int width = cells.Keys.Max() + 1;
            var values = Enumerable.Repeat(string.Empty, width).ToArray();
            foreach (var cell in cells)
            {
                values[cell.Key] = cell.Value;
            }

            AddRowIfNotEmpty(rows, values.ToList());
        }

        return rows;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);

        return document
            .Descendants(main + "si")
            .Select(item => string.Concat(item.Descendants(main + "t").Select(text => text.Value)))
            .ToList();
    }

    private static ZipArchiveEntry FindFirstWorksheet(ZipArchive archive)
    {
        ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
        if (workbookEntry is not null)
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

            using Stream workbookStream = workbookEntry.Open();
            XDocument workbook = XDocument.Load(workbookStream);
            XElement? firstSheet = workbook.Descendants(main + "sheet").FirstOrDefault();
            string? relationshipId = firstSheet?.Attribute(relationships + "id")?.Value;

            ZipArchiveEntry? relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (!string.IsNullOrWhiteSpace(relationshipId) && relationshipsEntry is not null)
            {
                using Stream relationshipsStream = relationshipsEntry.Open();
                XDocument relationshipDocument = XDocument.Load(relationshipsStream);
                string? target = relationshipDocument
                    .Descendants(packageRelationships + "Relationship")
                    .FirstOrDefault(item => (string?)item.Attribute("Id") == relationshipId)
                    ?.Attribute("Target")?.Value;

                string? normalizedTarget = NormalizeWorksheetPath(target);
                if (normalizedTarget is not null)
                {
                    ZipArchiveEntry? resolved = archive.GetEntry(normalizedTarget);
                    if (resolved is not null) return resolved;
                }
            }
        }

        return archive.Entries
            .FirstOrDefault(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                                  && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("El XLSX no contiene una hoja de cálculo válida.");
    }

    private static string? NormalizeWorksheetPath(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        string normalized = target.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return normalized;
        return "xl/" + normalized;
    }

    private static string ReadCellValue(XElement cell, XNamespace main, IReadOnlyList<string> sharedStrings)
    {
        string type = cell.Attribute("t")?.Value ?? string.Empty;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(main + "t").Select(text => text.Value));
        }

        string rawValue = cell.Element(main + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
        }

        return rawValue;
    }

    private static int GetColumnIndex(string reference)
    {
        int result = 0;
        foreach (char current in reference)
        {
            if (!char.IsLetter(current)) break;
            result = result * 26 + (char.ToUpperInvariant(current) - 'A' + 1);
        }

        return Math.Max(0, result - 1);
    }

    private static IReadOnlyList<MassCommunicationRecipientImportRecord> MapRows(List<string[]> rows)
    {
        if (rows.Count > MaxRows)
        {
            throw new ArgumentException($"El archivo no puede superar las {MaxRows:N0} filas.");
        }

        if (rows.Count == 0) return [];

        string[] firstRow = rows[0];
        int nameIndex = FindHeaderIndex(firstRow, ["nombre", "name", "empresa", "inmobiliaria", "razonsocial", "company", "contacto"]);
        int emailIndex = FindHeaderIndex(firstRow, ["email", "correo", "mail", "correoelectronico", "emailaddress"]);
        int phoneIndex = FindHeaderIndex(firstRow, ["telefono", "tel", "celular", "whatsapp", "phone", "movil", "mobile"]);
        bool hasHeader = nameIndex >= 0 || emailIndex >= 0 || phoneIndex >= 0;

        int dataStart = hasHeader ? 1 : 0;
        if (!hasHeader)
        {
            nameIndex = 0;
            emailIndex = 1;
            phoneIndex = 2;
        }

        var result = new List<MassCommunicationRecipientImportRecord>();
        for (int i = dataStart; i < rows.Count; i++)
        {
            string[] row = rows[i];
            string? name = GetValue(row, nameIndex);
            string? email = GetValue(row, emailIndex);
            string? phone = GetValue(row, phoneIndex);

            result.Add(new MassCommunicationRecipientImportRecord
            {
                RowNumber = i + 1,
                Name = name,
                Email = email,
                Phone = phone,
                EmailKey = NormalizeEmail(email)
            });
        }

        return result;
    }

    private static int FindHeaderIndex(string[] row, IReadOnlyCollection<string> candidates)
    {
        for (int index = 0; index < row.Length; index++)
        {
            string normalized = NormalizeHeader(row[index]);
            if (candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return index;
        }

        return -1;
    }

    private static string? GetValue(string[] row, int index)
    {
        if (index < 0 || index >= row.Length) return null;
        string value = row[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (char current in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(current) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(current)) builder.Append(current);
        }

        return builder.ToString();
    }
}
