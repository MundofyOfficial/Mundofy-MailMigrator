namespace Mundofy.MailMigrator.Core.Models;

public class MigrationOptions
{
    public int MaxConcurrency { get; set; } = 4;
    public List<string> SkipFolders { get; set; } = new();
    public List<string> CopyFolders { get; set; } = new();
    public List<string> SkipPrefixes { get; set; } = new();
    public List<string> AllowFlags { get; set; } = new();
    public List<string> DenyFlags { get; set; } = new() { "\\Recent" };
    public Dictionary<string, string> TimezoneConversions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "UTC", "+0000" },
        { "UT", "+0000" }
    };

    public bool CreateEmptyFolders { get; set; } = false;
    public bool SubscribeFolders { get; set; } = false;
    public bool CopyAllMessagesToInbox { get; set; } = false;
    public bool Deduplicate { get; set; } = true;
    public string DstRootFolder { get; set; } = string.Empty;

    public bool ShouldCopyFolder(string folderName)
    {
        if (CopyFolders.Count > 0 && !CopyFolders.Any(f => f.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (SkipFolders.Any(f => f.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (SkipPrefixes.Any(p => folderName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }
}
