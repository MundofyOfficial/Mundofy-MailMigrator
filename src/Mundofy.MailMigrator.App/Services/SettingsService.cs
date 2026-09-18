using System.IO;
using System.Text.Json;

namespace Mundofy.MailMigrator.App.Services;

public class AppSettings
{
    // Single Account Migration
    public string SingleSourceHost { get; set; } = "";
    public int SingleSourcePort { get; set; } = 993;
    public string SingleSourceProtocol { get; set; } = "Imap";
    public bool SingleSourceUseSsl { get; set; } = true;
    public string SingleSourceUser { get; set; } = "";

    public string SingleDestHost { get; set; } = "";
    public int SingleDestPort { get; set; } = 993;
    public string SingleDestProtocol { get; set; } = "Imap";
    public bool SingleDestUseSsl { get; set; } = true;
    public string SingleDestUser { get; set; } = "";

    // Batch Migration Defaults
    public string BatchSourceProtocol { get; set; } = "Imap";
    public string BatchSourceHost { get; set; } = "";
    public int BatchSourcePort { get; set; } = 993;
    public string BatchDestProtocol { get; set; } = "Imap";
    public string BatchDestHost { get; set; } = "";
    public int BatchDestPort { get; set; } = 993;
    public int Concurrency { get; set; } = 4;

    // Advanced Migration Options
    public bool Deduplicate { get; set; } = true;
    public bool AllowInvalidCertificates { get; set; } = false;
    public bool PreventSleepDuringMigration { get; set; } = true;
    public string DstRootFolder { get; set; } = "";
    public string SkipFolders { get; set; } = "Trash, Junk, Spam, Deleted Items";
    public string DenyFlags { get; set; } = "\\Recent";

    // Date Range Filter
    public bool EnableDateFilter { get; set; } = false;
    public string SelectedDatePreset { get; set; } = "All Emails (No date limit)";
    public string SinceDateText { get; set; } = "";
    public string BeforeDateText { get; set; } = "";

    // Privacy Preferences
    public bool MaskPasswords { get; set; } = true;
}

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Mundofy",
        "MailMigrator"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Silently fall back to defaults if settings file is corrupt or unreadable
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore background save failures (e.g. disk write lock)
        }
    }
}
