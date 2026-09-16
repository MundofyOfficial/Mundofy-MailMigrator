using System.Text;
using System.Text.RegularExpressions;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Parsers;

public class ImapCopyCfgResult
{
    public ServerEndpoint SourceEndpoint { get; set; } = new() { Port = 993, UseSsl = true };
    public ServerEndpoint DestEndpoint { get; set; } = new() { Port = 993, UseSsl = true };
    public MigrationOptions Options { get; set; } = new();
    public List<AccountJob> Accounts { get; set; } = new();
}

public static class ImapCopyCfgParser
{
    public static ImapCopyCfgResult ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    public static ImapCopyCfgResult Parse(string configText)
    {
        var result = new ImapCopyCfgResult();
        using var reader = new StringReader(configText);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                continue;

            var tokens = Tokenize(line);
            if (tokens.Count == 0) continue;

            var cmd = tokens[0].ToLowerInvariant();
            switch (cmd)
            {
                case "sourceserver":
                    if (tokens.Count > 1) result.SourceEndpoint.Host = tokens[1];
                    break;
                case "sourceport":
                    if (tokens.Count > 1 && int.TryParse(tokens[1], out int sPort))
                    {
                        result.SourceEndpoint.Port = sPort;
                        result.SourceEndpoint.UseSsl = (sPort == 993 || sPort == 995);
                    }
                    break;
                case "sourceprotocol":
                    if (tokens.Count > 1)
                    {
                        if (tokens[1].Equals("pop3", StringComparison.OrdinalIgnoreCase) || tokens[1].Equals("pop", StringComparison.OrdinalIgnoreCase))
                            result.SourceEndpoint.Protocol = ServerProtocol.Pop3;
                        else
                            result.SourceEndpoint.Protocol = ServerProtocol.Imap;
                    }
                    break;
                case "destserver":
                    if (tokens.Count > 1) result.DestEndpoint.Host = tokens[1];
                    break;
                case "destport":
                    if (tokens.Count > 1 && int.TryParse(tokens[1], out int dPort))
                    {
                        result.DestEndpoint.Port = dPort;
                        result.DestEndpoint.UseSsl = (dPort == 993);
                    }
                    break;
                case "skipfolder":
                    if (tokens.Count > 1) result.Options.SkipFolders.Add(tokens[1]);
                    break;
                case "skipmatch":
                    if (tokens.Count > 1) result.Options.SkipPrefixes.Add(tokens[1]);
                    break;
                case "copyfolder":
                    if (tokens.Count > 1) result.Options.CopyFolders.Add(tokens[1]);
                    break;
                case "dstrootfolder":
                    if (tokens.Count > 1) result.Options.DstRootFolder = tokens[1];
                    break;
                case "allowflags":
                    if (tokens.Count > 1)
                        result.Options.AllowFlags.AddRange(tokens[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                case "denyflags":
                    if (tokens.Count > 1)
                        result.Options.DenyFlags.AddRange(tokens[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                case "converttimezone":
                    if (tokens.Count > 2)
                        result.Options.TimezoneConversions[tokens[1]] = tokens[2];
                    break;
                case "createemptyfolders":
                    result.Options.CreateEmptyFolders = true;
                    break;
                case "subscribefolder":
                case "subscribesrcfolder":
                    result.Options.SubscribeFolders = true;
                    break;
                case "concurrency":
                case "threads":
                case "workers":
                    if (tokens.Count > 1 && int.TryParse(tokens[1], out int conc))
                        result.Options.MaxConcurrency = Math.Clamp(conc, 1, 32);
                    break;
                case "copy":
                    if (tokens.Count >= 5)
                    {
                        var srcUser = tokens[1];
                        var srcPass = tokens[2];
                        var dstUser = tokens[3] == "*" ? srcUser : tokens[3];
                        var dstPass = tokens[4] == "*" ? srcPass : tokens[4];

                        result.Accounts.Add(new AccountJob
                        {
                            SourceUser = srcUser,
                            SourcePassword = srcPass,
                            DestUser = dstUser,
                            DestPassword = dstPass,
                            Status = MigrationStatus.Ready,
                            StatusMessage = "Loaded from config"
                        });
                    }
                    break;
            }
        }

        return result;
    }

    public static string GenerateConfig(ServerEndpoint source, ServerEndpoint dest, MigrationOptions options, IEnumerable<AccountJob> accounts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#############################################################");
        sb.AppendLine("# Mundofy MailMigrator Configuration File");
        sb.AppendLine($"# Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("#############################################################");
        sb.AppendLine();
        sb.AppendLine("# Sourceserver");
        sb.AppendLine($"SourceProtocol {(source.Protocol == ServerProtocol.Pop3 ? "POP3" : "IMAP")}");
        sb.AppendLine($"SourceServer {source.Host}");
        sb.AppendLine($"SourcePort {source.Port}");
        sb.AppendLine();
        sb.AppendLine("# Destinationserver");
        sb.AppendLine($"DestServer {dest.Host}");
        sb.AppendLine($"DestPort {dest.Port}");
        sb.AppendLine();
        sb.AppendLine("# Concurrency settings (number of parallel account migrations)");
        sb.AppendLine($"Concurrency {options.MaxConcurrency}");
        sb.AppendLine();

        if (options.SkipFolders.Count > 0)
        {
            sb.AppendLine("# Folders to skip");
            foreach (var f in options.SkipFolders)
                sb.AppendLine($"skipfolder \"{f}\"");
            sb.AppendLine();
        }

        if (options.CopyFolders.Count > 0)
        {
            sb.AppendLine("# Folders to copy");
            foreach (var f in options.CopyFolders)
                sb.AppendLine($"copyfolder \"{f}\"");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(options.DstRootFolder))
            sb.AppendLine($"DstRootFolder \"{options.DstRootFolder}\"");

        if (options.DenyFlags.Count > 0)
            sb.AppendLine($"DenyFlags \"{string.Join(" ", options.DenyFlags)}\"");

        sb.AppendLine();
        sb.AppendLine("#############################");
        sb.AppendLine("# List of users and passwords");
        sb.AppendLine("#############################");
        sb.AppendLine("#       SourceUser    SourcePassword   DestinationUser DestinationPassword");

        foreach (var acc in accounts)
        {
            sb.AppendLine($"Copy    \"{acc.SourceUser}\"    \"{acc.SourcePassword}\"    \"{acc.DestUser}\"    \"{acc.DestPassword}\"");
        }

        return sb.ToString();
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var pattern = @"[^\s""]+|""([^""]*)""";
        var matches = Regex.Matches(line, pattern);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
                tokens.Add(match.Groups[1].Value);
            else
                tokens.Add(match.Value);
        }

        return tokens;
    }
}
