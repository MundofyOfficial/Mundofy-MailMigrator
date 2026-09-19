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

    [Fact]
    public void PauseAccount_WhenAccountNotMigrating_ReturnsFalse()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);
        var account = new AccountJob
        {
            SourceUser = "inactive@source.com",
            DestUser = "inactive@dest.com"
        };

        bool result = orchestrator.PauseAccount(account);

        Assert.False(result);
    }

    [Fact]
    public async Task EnqueueAccount_WhenResumingPaused_PreservesTotalAccountCount()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);

        var src = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var dst = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var options = new MigrationOptions { MaxConcurrency = 1 };

        var initial = new List<AccountJob>
        {
            new() { SourceUser = "user1@source.com", DestUser = "user1@dest.com" }
        };

        var runTask = orchestrator.RunBatchAsync(src, dst, initial, options);
        await Task.Delay(100);

        var pausedAccount = new AccountJob
        {
            SourceUser = "user1@source.com",
            DestUser = "user1@dest.com",
            Status = MigrationStatus.Paused
        };

        int progressReportedTotal = 0;
        orchestrator.BatchProgressUpdated += (s, e) =>
        {
            progressReportedTotal = e.TotalAccounts;
        };

        bool enqueued = orchestrator.EnqueueAccount(pausedAccount);
        Assert.True(enqueued);
        Assert.Equal(MigrationStatus.Queued, pausedAccount.Status);
        // Total accounts should remain 1, not incremented to 2
        Assert.Equal(1, progressReportedTotal);

        orchestrator.Stop();
        await runTask;
    }

    [Fact]
    public async Task RunBatchAsync_WhenAccountsCompleted_ResetsCountersAndReruns()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);

        var src = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var dst = new ServerEndpoint { Host = "127.0.0.1", Port = 993, UseSsl = true };
        var options = new MigrationOptions { MaxConcurrency = 1 };

        var completedAccount = new AccountJob
        {
            SourceUser = "done@source.com",
            DestUser = "done@dest.com",
            Status = MigrationStatus.Completed,
            StatusMessage = "Completed previously",
            TotalMessages = 500,
            CopiedMessages = 500,
            BytesTransferred = 1048576,
            TransferSpeed = "5 MB/s"
        };

        var accounts = new List<AccountJob> { completedAccount };

        var runTask = orchestrator.RunBatchAsync(src, dst, accounts, options);
        await Task.Delay(50);

        // Account counters must be cleanly reset for the new request
        Assert.True(completedAccount.Status == MigrationStatus.Queued || completedAccount.Status == MigrationStatus.InProgress);
        Assert.Equal(0, completedAccount.TotalMessages);
        Assert.Equal(0, completedAccount.CopiedMessages);
        Assert.Equal(0, completedAccount.BytesTransferred);
        Assert.Equal(string.Empty, completedAccount.TransferSpeed);

        orchestrator.Stop();
        await runTask;
    }
}
