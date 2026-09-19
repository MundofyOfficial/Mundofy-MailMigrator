using Mundofy.MailMigrator.Core.Models;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class MailboxQuotaInfoTests
{
    [Fact]
    public void FormatBytes_FormatsCorrectly()
    {
        Assert.Equal("0 B", MailboxQuotaInfo.FormatBytes(0));
        Assert.Equal("0 B", MailboxQuotaInfo.FormatBytes(-50));
        Assert.Equal("500 B", MailboxQuotaInfo.FormatBytes(500));
        Assert.Equal("1 KB", MailboxQuotaInfo.FormatBytes(1024));
        Assert.Equal("1.5 MB", MailboxQuotaInfo.FormatBytes((long)(1.5 * 1024 * 1024)));
        Assert.Equal("2 GB", MailboxQuotaInfo.FormatBytes(2L * 1024 * 1024 * 1024));
        Assert.Equal("1.25 TB", MailboxQuotaInfo.FormatBytes((long)(1.25 * 1024 * 1024 * 1024 * 1024)));
    }

    [Fact]
    public void PercentageAndAvailable_CalculatesAccurately()
    {
        var quota = new MailboxQuotaInfo
        {
            IsSupported = true,
            StorageUsedBytes = 500 * 1024 * 1024,
            StorageLimitBytes = 1000 * 1024 * 1024
        };

        Assert.Equal(50.0, quota.StoragePercentUsed);
        Assert.Equal(500 * 1024 * 1024, quota.StorageAvailableBytes);
        Assert.Equal(QuotaStatusLevel.Normal, quota.StatusLevel);
    }

    [Fact]
    public void StatusLevel_ThresholdsTriggerCorrectly()
    {
        long limit = 1000 * 1024 * 1024; // 1000 MB

        var normal = new MailboxQuotaInfo { IsSupported = true, StorageLimitBytes = limit, StorageUsedBytes = 799 * 1024 * 1024 };
        Assert.Equal(QuotaStatusLevel.Normal, normal.StatusLevel);

        var warning = new MailboxQuotaInfo { IsSupported = true, StorageLimitBytes = limit, StorageUsedBytes = 800 * 1024 * 1024 };
        Assert.Equal(QuotaStatusLevel.Warning, warning.StatusLevel);

        var critical = new MailboxQuotaInfo { IsSupported = true, StorageLimitBytes = limit, StorageUsedBytes = 950 * 1024 * 1024 };
        Assert.Equal(QuotaStatusLevel.Critical, critical.StatusLevel);

        var exceeded = new MailboxQuotaInfo { IsSupported = true, StorageLimitBytes = limit, StorageUsedBytes = 1050 * 1024 * 1024 };
        Assert.Equal(QuotaStatusLevel.Exceeded, exceeded.StatusLevel);
        Assert.Equal(0, exceeded.StorageAvailableBytes);
    }

    [Fact]
    public void StatusLevel_UnlimitedAndUnsupported()
    {
        var unsupported = new MailboxQuotaInfo { IsSupported = false };
        Assert.Equal(QuotaStatusLevel.Unknown, unsupported.StatusLevel);

        var unlimited = new MailboxQuotaInfo { IsSupported = true, StorageUsedBytes = 5000, StorageLimitBytes = null };
        Assert.Equal(QuotaStatusLevel.Unlimited, unlimited.StatusLevel);
    }

    [Fact]
    public void FormattedSummary_RendersHumanReadable()
    {
        var quota = new MailboxQuotaInfo
        {
            IsSupported = true,
            StorageUsedBytes = 2L * 1024 * 1024 * 1024,
            StorageLimitBytes = 10L * 1024 * 1024 * 1024,
            MessageCountUsed = 1250
        };

        var summary = quota.FormattedSummary;
        Assert.Contains("2 GB / 10 GB", summary);
        Assert.Contains("20.0%", summary);
        Assert.Contains("1,250 msgs", summary);
    }
}
