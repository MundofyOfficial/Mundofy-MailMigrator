using System.Collections.Concurrent;
using System.Diagnostics;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Services;

public class BatchProgressReport
{
    public int TotalAccounts { get; set; }
    public int CompletedAccounts { get; set; }
    public int FailedAccounts { get; set; }
    public int ActiveWorkers { get; set; }
    public int TotalCopiedMessages { get; set; }
    public int TotalFailedMessages { get; set; }
    public long TotalBytesTransferred { get; set; }
    public double OverallPercentage => TotalAccounts > 0 ? (CompletedAccounts + FailedAccounts) * 100.0 / TotalAccounts : 0;
    public TimeSpan ElapsedTime { get; set; }
}

public class BatchOrchestrator
{
    private readonly ImapMigrationService _migrationService;
    private CancellationTokenSource? _cts;

    public event EventHandler<BatchProgressReport>? BatchProgressUpdated;
    public event EventHandler<LogEntry>? LogEmitted;

    public bool IsRunning { get; private set; }

    public BatchOrchestrator(ImapMigrationService migrationService)
    {
        _migrationService = migrationService;
        _migrationService.LogEmitted += (s, e) => LogEmitted?.Invoke(this, e);
    }

    public async Task<MigrationSummary> RunBatchAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        IEnumerable<AccountJob> accounts,
        MigrationOptions options,
        IProgress<AccountJob>? accountProgress = null)
    {
        _cts = new CancellationTokenSource();
        IsRunning = true;

        var accountList = accounts.ToList();
        var stopwatch = Stopwatch.StartNew();

        int totalAccounts = accountList.Count;
        int completed = 0;
        int failed = 0;
        int active = 0;
        int totalCopied = 0;
        int totalErrors = 0;
        long totalBytes = 0;

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Info,
            Message = $"Starting batch migration for {totalAccounts} accounts with concurrency limit = {options.MaxConcurrency} workers."
        });

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(options.MaxConcurrency, 1, 32),
            CancellationToken = _cts.Token
        };

        try
        {
            await Parallel.ForEachAsync(accountList, parallelOptions, async (account, ct) =>
            {
                Interlocked.Increment(ref active);
                NotifyProgress();

                try
                {
                    await _migrationService.MigrateAccountAsync(source, dest, account, options, accountProgress, ct);

                    if (account.Status == MigrationStatus.Completed)
                        Interlocked.Increment(ref completed);
                    else
                        Interlocked.Increment(ref failed);

                    Interlocked.Add(ref totalCopied, account.CopiedMessages);
                    Interlocked.Add(ref totalErrors, account.FailedMessages);
                    Interlocked.Add(ref totalBytes, account.BytesTransferred);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                    NotifyProgress();
                }
            });
        }
        catch (OperationCanceledException)
        {
            LogEmitted?.Invoke(this, new LogEntry
            {
                Level = LogLevel.Warning,
                Message = "Batch migration was stopped by user."
            });
        }
        finally
        {
            IsRunning = false;
        }

        stopwatch.Stop();

        var summary = new MigrationSummary
        {
            TotalAccounts = totalAccounts,
            SuccessfulAccounts = completed,
            FailedAccounts = failed,
            TotalMessagesCopied = totalCopied,
            TotalErrors = totalErrors,
            TotalBytesTransferred = totalBytes,
            ElapsedTime = stopwatch.Elapsed
        };

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Success,
            Message = $"Batch migration completed! Processed: {completed}/{totalAccounts} success, {totalCopied} messages copied in {stopwatch.Elapsed:hh\\:mm\\:ss}."
        });

        return summary;

        void NotifyProgress()
        {
            BatchProgressUpdated?.Invoke(this, new BatchProgressReport
            {
                TotalAccounts = totalAccounts,
                CompletedAccounts = completed,
                FailedAccounts = failed,
                ActiveWorkers = active,
                TotalCopiedMessages = totalCopied,
                TotalFailedMessages = totalErrors,
                TotalBytesTransferred = totalBytes,
                ElapsedTime = stopwatch.Elapsed
            });
        }
    }

    public async Task TestAllAccountsAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        IEnumerable<AccountJob> accounts,
        int concurrency = 4,
        CancellationToken ct = default)
    {
        var accountList = accounts.ToList();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(concurrency, 1, 16),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(accountList, parallelOptions, async (acc, token) =>
        {
            acc.Status = MigrationStatus.Testing;
            acc.StatusMessage = "Testing source connection...";

            var srcTest = await _migrationService.TestConnectionAsync(source, acc.SourceUser, acc.SourcePassword, token);
            if (!srcTest.Success)
            {
                acc.Status = MigrationStatus.Failed;
                acc.StatusMessage = $"Source Error: {srcTest.Message}";
                return;
            }

            acc.StatusMessage = "Testing destination connection...";
            var dstTest = await _migrationService.TestConnectionAsync(dest, acc.DestUser, acc.DestPassword, token);
            if (!dstTest.Success)
            {
                acc.Status = MigrationStatus.Failed;
                acc.StatusMessage = $"Dest Error: {dstTest.Message}";
                return;
            }

            acc.Status = MigrationStatus.Ready;
            acc.StatusMessage = "Verified (Ready)";
        });
    }

    public void Stop()
    {
        if (IsRunning && _cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            IsRunning = false;
        }
    }
}
