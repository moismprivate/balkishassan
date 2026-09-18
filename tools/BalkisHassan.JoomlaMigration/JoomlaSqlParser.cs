using System.Text;
using System.Text.RegularExpressions;

namespace BalkisHassan.JoomlaMigration;

public sealed class JoomlaSqlParser
{
    public IReadOnlyDictionary<string, List<JoomlaRow>> Parse(string path, params string[] requestedTables)
    {
        var utf8 = new UTF8Encoding(false, true);
        var sql = File.ReadAllText(path, utf8);
        var requested = requestedTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = requested.ToDictionary(x => x, _ => new List<JoomlaRow>(), StringComparer.OrdinalIgnoreCase);
        var position = 0;

        while ((position = sql.IndexOf("INSERT INTO `", position, StringComparison.Ordinal)) >= 0)
        {
            var tableStart = position + "INSERT INTO `".Length;
            var tableEnd = sql.IndexOf('`', tableStart);
            if (tableEnd < 0) break;
            var table = sql[tableStart..tableEnd];
            if (!requested.Contains(table))
            {
                position = tableEnd + 1;
                continue;
            }

            var columnsStart = sql.IndexOf('(', tableEnd);
            var columnsEnd = sql.IndexOf(')', columnsStart + 1);
            var valuesMarker = sql.IndexOf("VALUES", columnsEnd, StringComparison.OrdinalIgnoreCase);
            if (columnsStart < 0 || columnsEnd < 0 || valuesMarker < 0) throw new FormatException($"Ongeldige INSERT voor {table}.");

            var columns = Regex.Matches(sql[(columnsStart + 1)..columnsEnd], "`([^`]+)`")
                .Select(x => x.Groups[1].Value).ToArray();
            position = valuesMarker + "VALUES".Length;

            while (position < sql.Length)
            {
                SkipWhitespaceAndCommas(sql, ref position);
                if (position >= sql.Length || sql[position] == ';')
                {
                    position++;
                    break;
                }

                if (sql[position] != '(') throw new FormatException($"Verwachte rij voor {table} op positie {position}.");
                position++;
                var values = new string?[columns.Length];
                for (var index = 0; index < columns.Length; index++)
                {
                    SkipWhitespace(sql, ref position);
                    values[index] = ParseValue(sql, ref position);
                    SkipWhitespace(sql, ref position);
                    if (index < columns.Length - 1)
                    {
                        if (sql[position] != ',') throw new FormatException($"Verwachte komma in {table} op positie {position}.");
                        position++;
                    }
                }

                SkipWhitespace(sql, ref position);
                if (position >= sql.Length || sql[position] != ')') throw new FormatException($"Rij in {table} is niet afgesloten.");
                position++;
                result[table].Add(new JoomlaRow(columns, values));
            }
        }

        return result;
    }

    private static string? ParseValue(string sql, ref int position)
    {
        if (sql[position] == '\'') return ParseString(sql, ref position);
        var start = position;
        while (position < sql.Length && sql[position] is not ',' and not ')') position++;
        var raw = sql[start..position].Trim();
        return raw.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : raw;
    }

    private static string ParseString(string sql, ref int position)
    {
        position++;
        var value = new StringBuilder();
        while (position < sql.Length)
        {
            var character = sql[position++];
            if (character == '\'')
            {
                if (position < sql.Length && sql[position] == '\'')
                {
                    value.Append('\'');
                    position++;
                    continue;
                }
                return value.ToString();
            }

            if (character != '\\' || position >= sql.Length)
            {
                value.Append(character);
                continue;
            }

            var escaped = sql[position++];
            value.Append(escaped switch
            {
                '0' => '\0', 'b' => '\b', 'n' => '\n', 'r' => '\r', 't' => '\t',
                'Z' => '\u001A', '\\' => '\\', '\'' => '\'', '"' => '"', _ => escaped
            });
        }
        throw new FormatException("Niet-afgesloten SQL-string.");
    }

    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position])) position++;
    }

    private static void SkipWhitespaceAndCommas(string value, ref int position)
    {
        while (position < value.Length && (char.IsWhiteSpace(value[position]) || value[position] == ',')) position++;
    }
}

public sealed class JoomlaRow
{
    private readonly Dictionary<string, string?> _values;

    public JoomlaRow(IReadOnlyList<string> columns, IReadOnlyList<string?> values)
    {
        _values = columns.Select((column, index) => (column, values[index]))
            .ToDictionary(x => x.column, x => x.Item2, StringComparer.OrdinalIgnoreCase);
    }

    public string? Get(string name) => _values.GetValueOrDefault(name);
    public string Text(string name) => Get(name) ?? string.Empty;
    public int Int(string name) => int.TryParse(Get(name), out var value) ? value : 0;
    public bool Bool(string name) => Int(name) == 1;
}
