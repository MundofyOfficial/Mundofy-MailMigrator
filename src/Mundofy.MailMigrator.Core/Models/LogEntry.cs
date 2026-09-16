namespace Mundofy.MailMigrator.Core.Models;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}

public class MigrationSummary
{
    public int TotalAccounts { get; set; }
    public int SuccessfulAccounts { get; set; }
    public int FailedAccounts { get; set; }
    public int TotalMessagesCopied { get; set; }
    public int TotalErrors { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}
