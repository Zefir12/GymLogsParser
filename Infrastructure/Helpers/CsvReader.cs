using System.Text;

namespace Infrastructure.Helpers;

/// <summary>One CSV record and the 1-based line it starts on.</summary>
public sealed record CsvRecord(int Line, IReadOnlyList<string> Fields);

/// <summary>
/// A small RFC 4180 reader: comma-separated, fields optionally in double quotes,
/// <c>""</c> for a quote inside a quoted field, and line breaks allowed inside quotes
/// (Hevy notes can span lines). Accepts LF and CRLF and skips a leading BOM and blank
/// lines. An empty unquoted field and an empty quoted field both read as "".
/// </summary>
public static class CsvReader
{
    /// <summary>Reads every record. Throws <see cref="FormatException"/> when a quoted field never closes.</summary>
    public static IEnumerable<CsvRecord> Read(string text)
    {
        var pos = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        var line = 1;

        while (pos < text.Length)
        {
            var recordLine = line;
            var fields = new List<string>();
            var field = new StringBuilder();
            var endOfRecord = false;

            while (!endOfRecord)
            {
                if (pos < text.Length && text[pos] == '"')
                {
                    pos++;
                    while (true)
                    {
                        if (pos >= text.Length)
                        {
                            throw new FormatException($"A quoted field that starts on line {recordLine} is never closed.");
                        }

                        var c = text[pos];
                        if (c == '"')
                        {
                            if (pos + 1 < text.Length && text[pos + 1] == '"')
                            {
                                field.Append('"');
                                pos += 2;
                                continue;
                            }

                            pos++;
                            break;
                        }

                        if (c == '\n') line++;
                        field.Append(c);
                        pos++;
                    }
                }

                // Unquoted text, or anything after a closing quote up to the delimiter.
                while (pos < text.Length && text[pos] is not (',' or '\n' or '\r'))
                {
                    field.Append(text[pos]);
                    pos++;
                }

                fields.Add(field.ToString());
                field.Clear();

                if (pos >= text.Length)
                {
                    endOfRecord = true;
                }
                else if (text[pos] == ',')
                {
                    pos++;
                }
                else
                {
                    if (text[pos] == '\r') pos++;
                    if (pos < text.Length && text[pos] == '\n') pos++;
                    line++;
                    endOfRecord = true;
                }
            }

            if (fields is [""]) continue; // blank line
            yield return new CsvRecord(recordLine, fields);
        }
    }
}
