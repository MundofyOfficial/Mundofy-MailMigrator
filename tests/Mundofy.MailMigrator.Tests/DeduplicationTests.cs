using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Services;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class DeduplicationTests
{
    [Fact]
    public void TestMigrationOptions_DeduplicateDefaultIsTrue()
    {
        var options = new MigrationOptions();
        Assert.True(options.Deduplicate);
    }

    [Fact]
    public void TestAccountJob_ProgressWithSkippedMessages()
    {
        var job = new AccountJob
        {
            TotalMessages = 532,
            CopiedMessages = 3,
            SkippedMessages = 529,
            FailedMessages = 0
        };

        Assert.Equal(100.0, job.ProgressPercentage);
    }
}
