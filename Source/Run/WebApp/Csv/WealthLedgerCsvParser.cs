using System.Text;
using WealthLedger.Contracts.Api.Responses;

namespace WealthLedger.WebApp.Csv;

/// <summary>
/// Minimal RFC 4180-style CSV parsing (quoted fields, doubled quotes, CRLF).
/// </summary>
public static class WealthLedgerCsvParser
{
    public static CsvParseResult Parse(string rawText)
    {
        var result = new CsvParseResult();
        if (string.IsNullOrWhiteSpace(rawText))
            return result;

        // Strip UTF-8 BOM if present
        if (rawText.Length > 0 && rawText[0] == '\uFEFF')
            rawText = rawText[1..];

        using var reader = new StringReader(rawText);
        if (!TryReadRecord(reader, out var headerFields, out var eofAfterHeader))
        {
            result.ParseErrors.Add(new CsvImportRowError { LineNumber = 1, Message = "CSV is empty or invalid." });
            return result;
        }

        result.HeaderLineNumber = 1;
        for (var i = 0; i < headerFields.Count; i++)
            headerFields[i] = headerFields[i].Trim();

        result.Headers = headerFields;

        var lineNumber = 2;
        while (!eofAfterHeader)
        {
            if (!TryReadRecord(reader, out var cells, out eofAfterHeader))
                break;

            if (cells.Count != headerFields.Count)
            {
                result.ParseErrors.Add(new CsvImportRowError
                {
                    LineNumber = lineNumber,
                    Message = $"Expected {headerFields.Count} columns, found {cells.Count}."
                });
                lineNumber++;
                continue;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headerFields.Count; c++)
            {
                var key = headerFields[c].Trim();
                if (string.IsNullOrEmpty(key))
                    continue;
                dict[key] = cells[c].Trim();
            }

            if (IsBlankRow(dict))
            {
                result.SkippedBlankLines++;
                lineNumber++;
                continue;
            }

            result.Rows.Add((lineNumber, dict));
            lineNumber++;
        }

        return result;
    }

    private static bool IsBlankRow(Dictionary<string, string> dict) =>
        dict.Values.All(string.IsNullOrWhiteSpace);

    private static bool TryReadRecord(StringReader reader, out List<string> fields, out bool eof)
    {
        fields = new List<string>();
        eof = false;
        var field = new StringBuilder();
        var inQuotes = false;

        while (true)
        {
            var n = reader.Read();
            if (n == -1)
            {
                eof = true;
                fields.Add(field.ToString());
                if (inQuotes)
                    return false;
                // Meaningful record: multiple fields, or single non-empty field
                return fields.Count > 1 || fields.Any(f => f.Length > 0);
            }

            var ch = (char)n;

            if (inQuotes)
            {
                if (ch == '"')
                {
                    var peek = reader.Peek();
                    if (peek == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else
            {
                switch (ch)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        fields.Add(field.ToString());
                        return true;
                    default:
                        field.Append(ch);
                        break;
                }
            }
        }
    }
}

public class CsvParseResult
{
    public int HeaderLineNumber { get; set; } = 1;
    public List<string> Headers { get; set; } = new();
    public List<(int LineNumber, Dictionary<string, string> Cells)> Rows { get; set; } = new();
    public List<CsvImportRowError> ParseErrors { get; set; } = new();
    public int SkippedBlankLines { get; set; }
}

public static class CsvRowHelper
{
    public static string? GetCell(IReadOnlyDictionary<string, string> cells, string columnName)
    {
        foreach (var kv in cells)
        {
            if (string.Equals(kv.Key, columnName, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }
}
