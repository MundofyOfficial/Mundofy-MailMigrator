using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Services;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class BatchOrchestratorTests
{
    [Fact]
    public void EnqueueAccount_WhenNotRunning_ReturnsFalse()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);
        var account = new AccountJob
        {
            SourceUser = "late@source.com",
            DestUser = "late@dest.com"
        };

        bool result = orchestrator.EnqueueAccount(account);

        Assert.False(result);
        Assert.False(orchestrator.IsRunning);
    }

    [Fact]
    public async Task DynamicBatchMigration_CanEnqueueWhileRunning()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);

        var src = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var dst = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var options = new MigrationOptions { MaxConcurrency = 2 };

        var initial = new List<AccountJob>
        {
            new() { SourceUser = "user1@source.com", DestUser = "user1@dest.com" }
        };

        var runTask = orchestrator.RunBatchAsync(src, dst, initial, options);
        await Task.Delay(100);

        Assert.True(orchestrator.IsRunning);

        var dynamicAccount = new AccountJob
        {
            SourceUser = "dynamically_added@source.com",
            DestUser = "dynamically_added@dest.com"
        };

        bool enqueued = orchestrator.EnqueueAccount(dynamicAccount);

        Assert.True(enqueued);
        Assert.Equal(MigrationStatus.Queued, dynamicAccount.Status);

        orchestrator.Stop();
        var summary = await runTask;

        Assert.NotNull(summary);
    }
}
