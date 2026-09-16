using System.Text;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Parsers;

public static class CsvAccountParser
{
    public static List<AccountJob> Parse(string text)
    {
        var list = new List<AccountJob>();
        using var reader = new StringReader(text);
        string? line;
        bool isFirstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            // Auto-detect delimiter: comma, semicolon, or tab
            char delimiter = ',';
            if (line.Contains('\t')) delimiter = '\t';
            else if (line.Contains(';') && !line.Contains(',')) delimiter = ';';

            var parts = SplitCsvLine(line, delimiter);
            if (parts.Count < 2) continue;

            // Check if first line is a header
            if (isFirstLine)
            {
                isFirstLine = false;
                var first = parts[0].ToLowerInvariant();
                if (first.Contains("user") || first.Contains("email") || first.Contains("source") || first.Contains("login"))
                    continue; // skip header row
            }

            string srcUser = parts[0];
            string srcPass = parts.Count > 1 ? parts[1] : string.Empty;
            string dstUser = parts.Count > 2 ? parts[2] : srcUser;
            string dstPass = parts.Count > 3 ? parts[3] : srcPass;

            if (dstUser == "*") dstUser = srcUser;
            if (dstPass == "*") dstPass = srcPass;

            list.Add(new AccountJob
            {
                SourceUser = srcUser,
                SourcePassword = srcPass,
                DestUser = dstUser,
                DestPassword = dstPass,
                Status = MigrationStatus.Ready,
                StatusMessage = "Loaded from CSV/Text"
            });
        }

        return list;
    }

    public static string Export(IEnumerable<AccountJob> accounts, char delimiter = ',')
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SourceUser{delimiter}SourcePassword{delimiter}DestUser{delimiter}DestPassword{delimiter}Status{delimiter}CopiedMessages{delimiter}FailedMessages");

        foreach (var a in accounts)
        {
            sb.AppendLine($"\"{Escape(a.SourceUser)}\"{delimiter}\"{Escape(a.SourcePassword)}\"{delimiter}\"{Escape(a.DestUser)}\"{delimiter}\"{Escape(a.DestPassword)}\"{delimiter}\"{a.Status}\"{delimiter}{a.CopiedMessages}{delimiter}{a.FailedMessages}");
        }

        return sb.ToString();
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static string Escape(string s) => s.Replace("\"", "\"\"");
}
