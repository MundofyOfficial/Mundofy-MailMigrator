namespace Mundofy.MailMigrator.Core.Models;

public enum QuotaStatusLevel
{
    Unknown,
    Normal,
    Warning,   // >= 80%
    Critical,  // >= 95%
    Exceeded,  // >= 100%
    Unlimited
}

public class MailboxQuotaInfo
{
    public bool IsSupported { get; set; }
    public string QuotaRoot { get; set; } = string.Empty;

    public long? StorageUsedBytes { get; set; }
    public long? StorageLimitBytes { get; set; }

    public long? MessageCountUsed { get; set; }
    public long? MessageCountLimit { get; set; }

    public double? StoragePercentUsed
    {
        get
        {
            if (StorageUsedBytes.HasValue && StorageLimitBytes.HasValue && StorageLimitBytes.Value > 0)
            {
                return Math.Round((double)StorageUsedBytes.Value / StorageLimitBytes.Value * 100.0, 1);
            }
            return null;
        }
    }

    public long? StorageAvailableBytes
    {
        get
        {
            if (StorageUsedBytes.HasValue && StorageLimitBytes.HasValue)
            {
                long free = StorageLimitBytes.Value - StorageUsedBytes.Value;
                return free < 0 ? 0 : free;
            }
            return null;
        }
    }

    public QuotaStatusLevel StatusLevel
    {
        get
        {
            if (!IsSupported) return QuotaStatusLevel.Unknown;
            if (!StorageLimitBytes.HasValue || StorageLimitBytes.Value <= 0) return QuotaStatusLevel.Unlimited;

            double pct = StoragePercentUsed ?? 0;
            if (pct >= 100.0) return QuotaStatusLevel.Exceeded;
            if (pct >= 95.0) return QuotaStatusLevel.Critical;
            if (pct >= 80.0) return QuotaStatusLevel.Warning;
            return QuotaStatusLevel.Normal;
        }
    }

    public string FormattedSummary
    {
        get
        {
            if (!IsSupported)
            {
                if (MessageCountUsed.HasValue && StorageUsedBytes.HasValue)
                {
                    return $"{FormatBytes(StorageUsedBytes.Value)} ({MessageCountUsed.Value:N0} messages, server does not support quota query)";
                }
                if (MessageCountUsed.HasValue)
                {
                    return $"{MessageCountUsed.Value:N0} messages (quota not reported by server)";
                }
                return "Quota not supported by server";
            }

            if (StorageUsedBytes.HasValue && StorageLimitBytes.HasValue && StorageLimitBytes.Value > 0)
            {
                double pct = StoragePercentUsed ?? 0;
                string msgs = MessageCountUsed.HasValue ? $" • {MessageCountUsed.Value:N0} msgs" : "";
                return $"{FormatBytes(StorageUsedBytes.Value)} / {FormatBytes(StorageLimitBytes.Value)} ({pct:F1}%){msgs}";
            }

            if (StorageUsedBytes.HasValue)
            {
                string msgs = MessageCountUsed.HasValue ? $" • {MessageCountUsed.Value:N0} msgs" : "";
                return $"{FormatBytes(StorageUsedBytes.Value)} (Unlimited){msgs}";
            }

            if (!StorageLimitBytes.HasValue || StorageLimitBytes.Value <= 0)
            {
                string msgs = MessageCountUsed.HasValue ? $" ({MessageCountUsed.Value:N0} msgs)" : "";
                return $"Unlimited{msgs}";
            }

            return "Quota info unavailable";
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double val = bytes;
        int unitIndex = 0;
        while (val >= 1024.0 && unitIndex < units.Length - 1)
        {
            val /= 1024.0;
            unitIndex++;
        }
        return $"{val:0.##} {units[unitIndex]}";
    }
}
