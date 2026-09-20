using System.Text;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Parsers;

public static class CsvAccountParser
{
    public static List<AccountJob> Parse(string text, ServerProtocol? srcProto = null, ServerProtocol? dstProto = null)
    {
        var list = new List<AccountJob>();
        using var reader = new StringReader(text);
        string? line;
        bool isFirstLine = true;

        int srcUserIdx = -1;
        int srcPassIdx = -1;
        int dstUserIdx = -1;
        int dstPassIdx = -1;
        bool hasNamedHeaders = false;

        bool srcEnterprise = srcProto is ServerProtocol.Microsoft365 or ServerProtocol.GoogleWorkspace;
        bool dstEnterprise = dstProto is ServerProtocol.Microsoft365 or ServerProtocol.GoogleWorkspace;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            // Auto-detect delimiter: comma, semicolon, or tab
            char delimiter = ',';
            if (line.Contains('\t')) delimiter = '\t';
            else if (line.Contains(';') && !line.Contains(',')) delimiter = ';';

            var parts = SplitCsvLine(line, delimiter);
            if (parts.Count == 0) continue;

            // Check if first line is a header
            if (isFirstLine)
            {
                isFirstLine = false;
                if (TryDetectHeaders(parts, out srcUserIdx, out srcPassIdx, out dstUserIdx, out dstPassIdx))
                {
                    hasNamedHeaders = true;
                    continue; // Header row consumed
                }
            }

            string srcUser = "";
            string srcPass = "";
            string dstUser = "";
            string dstPass = "";

            if (hasNamedHeaders)
            {
                srcUser = (srcUserIdx >= 0 && srcUserIdx < parts.Count) ? parts[srcUserIdx] : "";
                srcPass = (srcPassIdx >= 0 && srcPassIdx < parts.Count) ? parts[srcPassIdx] : "";
                dstUser = (dstUserIdx >= 0 && dstUserIdx < parts.Count) ? parts[dstUserIdx] : "";
                dstPass = (dstPassIdx >= 0 && dstPassIdx < parts.Count) ? parts[dstPassIdx] : "";

                if (string.IsNullOrWhiteSpace(dstUser)) dstUser = srcUser;
                if (string.IsNullOrWhiteSpace(dstPass) && !dstEnterprise) dstPass = srcPass;
            }
            else
            {
                // Headerless heuristic parsing based on column count and protocol context
                if (parts.Count == 1)
                {
                    // Single email address per line (e.g. enterprise list of mailboxes)
                    srcUser = parts[0];
                    dstUser = parts[0];
                }
                else if (parts.Count == 2)
                {
                    // 2 columns: Could be (SrcUser, DestUser) OR (SrcUser, SrcPass)
                    // If second column contains '@' or either protocol is Enterprise M365/Google:
                    if (parts[1].Contains('@') || srcEnterprise || dstEnterprise)
                    {
                        srcUser = parts[0];
                        dstUser = parts[1];
                    }
                    else
                    {
                        // Classic (SrcUser, SrcPass) where Dest mirrors Src
                        srcUser = parts[0];
                        srcPass = parts[1];
                        dstUser = srcUser;
                        dstPass = srcPass;
                    }
                }
                else if (parts.Count == 3)
                {
                    if (srcEnterprise && !dstEnterprise)
                    {
                        // Source M365/Google (no pwd) -> Dest IMAP (with pwd)
                        // (SrcMailbox, DstUser, DstPass)
                        srcUser = parts[0];
                        dstUser = parts[1];
                        dstPass = parts[2];
                    }
                    else if (!srcEnterprise && dstEnterprise)
                    {
                        // Source IMAP (with pwd) -> Dest M365/Google (no pwd)
                        // (SrcUser, SrcPass, DstMailbox)
                        srcUser = parts[0];
                        srcPass = parts[1];
                        dstUser = parts[2];
                    }
                    else
                    {
                        // Default fallback: (SrcUser, SrcPass, DstUser)
                        srcUser = parts[0];
                        srcPass = parts[1];
                        dstUser = parts[2];
                        dstPass = srcPass;
                    }
                }
                else // 4 or more columns
                {
                    srcUser = parts[0];
                    srcPass = parts[1];
                    dstUser = parts[2];
                    dstPass = parts[3];
                }
            }

            if (dstUser == "*") dstUser = srcUser;
            if (dstPass == "*") dstPass = srcPass;

            if (string.IsNullOrWhiteSpace(srcUser) && string.IsNullOrWhiteSpace(dstUser))
                continue;

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

    private static bool TryDetectHeaders(
        List<string> parts,
        out int srcUserIdx,
        out int srcPassIdx,
        out int dstUserIdx,
        out int dstPassIdx)
    {
        srcUserIdx = -1;
        srcPassIdx = -1;
        dstUserIdx = -1;
        dstPassIdx = -1;

        bool foundAny = false;

        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i].Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace("/", "");

            if (p is "sourceuser" or "sourceemail" or "sourcemailbox" or "srcuser" or "srcemail" or "from" or "source")
            {
                srcUserIdx = i;
                foundAny = true;
            }
            else if (p is "sourcepassword" or "sourcepass" or "srcpass" or "srcpassword" or "sourcepwd" or "srcpwd")
            {
                srcPassIdx = i;
                foundAny = true;
            }
            else if (p is "destuser" or "destemail" or "destmailbox" or "destinationuser" or "destinationemail" or "dstuser" or "dstemail" or "to" or "destination" or "dest" or "target")
            {
                dstUserIdx = i;
                foundAny = true;
            }
            else if (p is "destpassword" or "destpass" or "destinationpassword" or "dstpass" or "dstpassword" or "destpwd" or "dstpwd")
            {
                dstPassIdx = i;
                foundAny = true;
            }
            else if (p is "user" or "email" or "login" or "username" or "mailbox")
            {
                if (srcUserIdx == -1)
                {
                    srcUserIdx = i;
                    foundAny = true;
                }
                else if (dstUserIdx == -1)
                {
                    dstUserIdx = i;
                    foundAny = true;
                }
            }
            else if (p is "password" or "pass" or "pwd")
            {
                if (srcPassIdx == -1)
                {
                    srcPassIdx = i;
                    foundAny = true;
                }
                else if (dstPassIdx == -1)
                {
                    dstPassIdx = i;
                    foundAny = true;
                }
            }
        }

        return foundAny;
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
