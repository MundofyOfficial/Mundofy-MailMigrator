using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
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
    private Channel<AccountJob>? _queue;
    private readonly object _finishLock = new();
    private CancellationTokenSource? _debounceCts;

    private int _totalAccounts;
    private int _completedAccounts;
    private int _failedAccounts;
    private int _activeWorkers;
    private int _totalCopiedMessages;
    private int _totalFailedMessages;
    private long _totalBytesTransferred;
    private Stopwatch? _stopwatch;

    public event EventHandler<BatchProgressReport>? BatchProgressUpdated;
    public event EventHandler<LogEntry>? LogEmitted;

    public bool IsRunning { get; private set; }

    public BatchOrchestrator(ImapMigrationService migrationService)
    {
        _migrationService = migrationService;
        _migrationService.LogEmitted += (s, e) => LogEmitted?.Invoke(this, e);
    }

    /// <summary>
    /// Dynamically enqueues a new account into an active, running batch migration without stopping it.
    /// </summary>
    public bool EnqueueAccount(AccountJob account)
    {
        if (!IsRunning || _queue == null || _cts == null || _cts.IsCancellationRequested)
            return false;

        lock (_finishLock)
        {
            _debounceCts?.Cancel();
        }

        account.Status = MigrationStatus.Queued;
        account.StatusMessage = "Queued in active batch...";
        account.TotalMessages = 0;
        account.CopiedMessages = 0;
        account.SkippedMessages = 0;
        account.FailedMessages = 0;
        account.BytesTransferred = 0;

        Interlocked.Increment(ref _totalAccounts);
        _queue.Writer.TryWrite(account);
        NotifyProgress();

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Success,
            Message = $"⚡ Dynamically added '{account.SourceUser}' to active running batch (batch total: {_totalAccounts})."
        });

        return true;
    }

    public async Task<MigrationSummary> RunBatchAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        IEnumerable<AccountJob> accounts,
        MigrationOptions options,
        IProgress<AccountJob>? accountProgress = null)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsRunning = true;

        _queue = Channel.CreateUnbounded<AccountJob>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // Pick accounts that are not already Completed
        var initialList = accounts.Where(a => a.Status != MigrationStatus.Completed).ToList();
        _totalAccounts = initialList.Count;
        _completedAccounts = 0;
        _failedAccounts = 0;
        _activeWorkers = 0;
        _totalCopiedMessages = 0;
        _totalFailedMessages = 0;
        _totalBytesTransferred = 0;

        foreach (var acc in initialList)
        {
            acc.Status = MigrationStatus.Queued;
            acc.StatusMessage = "Queued";
            _queue.Writer.TryWrite(acc);
        }

        _stopwatch = Stopwatch.StartNew();

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Info,
            Message = $"Starting batch migration for {_totalAccounts} accounts with concurrency limit = {options.MaxConcurrency} workers."
        });

        int workerCount = Math.Clamp(options.MaxConcurrency, 1, 32);
        var workerTasks = new List<Task>();

        void ScheduleCompletionCheck()
        {
            lock (_finishLock)
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1500, token);
                        lock (_finishLock)
                        {
                            if (!token.IsCancellationRequested &&
                                _queue != null &&
                                _queue.Reader.Count == 0 &&
                                Volatile.Read(ref _activeWorkers) == 0)
                            {
                                _queue.Writer.TryComplete();
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }, token);
            }
        }

        for (int i = 0; i < workerCount; i++)
        {
            workerTasks.Add(Task.Run(async () =>
            {
                try
                {
                    while (await _queue.Reader.WaitToReadAsync(ct))
                    {
                        while (_queue.Reader.TryRead(out var account))
                        {
                            Interlocked.Increment(ref _activeWorkers);
                            NotifyProgress();

                            try
                            {
                                await _migrationService.MigrateAccountAsync(source, dest, account, options, accountProgress, ct);

                                if (account.Status == MigrationStatus.Completed)
                                    Interlocked.Increment(ref _completedAccounts);
                                else
                                    Interlocked.Increment(ref _failedAccounts);

                                Interlocked.Add(ref _totalCopiedMessages, account.CopiedMessages);
                                Interlocked.Add(ref _totalFailedMessages, account.FailedMessages);
                                Interlocked.Add(ref _totalBytesTransferred, account.BytesTransferred);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _activeWorkers);
                                NotifyProgress();
                                ScheduleCompletionCheck();
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // worker cancelled gracefully
                }
            }, ct));
        }

        ScheduleCompletionCheck();

        try
        {
            await Task.WhenAll(workerTasks);
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
            _queue = null;
        }

        _stopwatch.Stop();

        var summary = new MigrationSummary
        {
            TotalAccounts = _totalAccounts,
            SuccessfulAccounts = _completedAccounts,
            FailedAccounts = _failedAccounts,
            TotalMessagesCopied = _totalCopiedMessages,
            TotalErrors = _totalFailedMessages,
            TotalBytesTransferred = _totalBytesTransferred,
            ElapsedTime = _stopwatch.Elapsed
        };

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Success,
            Message = $"Batch migration completed! Processed: {_completedAccounts}/{_totalAccounts} success, {_totalCopiedMessages} messages copied in {_stopwatch.Elapsed:hh\\:mm\\:ss}."
        });

        return summary;
    }

    private void NotifyProgress()
    {
        BatchProgressUpdated?.Invoke(this, new BatchProgressReport
        {
            TotalAccounts = _totalAccounts,
            CompletedAccounts = _completedAccounts,
            FailedAccounts = _failedAccounts,
            ActiveWorkers = _activeWorkers,
            TotalCopiedMessages = _totalCopiedMessages,
            TotalFailedMessages = _totalFailedMessages,
            TotalBytesTransferred = _totalBytesTransferred,
            ElapsedTime = _stopwatch?.Elapsed ?? TimeSpan.Zero
        });
    }

    public async Task<bool> TestSingleAccountAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        AccountJob acc,
        CancellationToken ct = default)
    {
        acc.Status = MigrationStatus.Testing;
        acc.StatusMessage = "Testing source connection...";

        var srcTest = await _migrationService.TestConnectionAsync(source, acc.SourceUser, acc.SourcePassword, ct);
        if (!srcTest.Success)
        {
            acc.Status = MigrationStatus.Failed;
            acc.StatusMessage = $"Source Error: {srcTest.Message}";
            return false;
        }

        acc.StatusMessage = "Testing destination connection...";
        var dstTest = await _migrationService.TestConnectionAsync(dest, acc.DestUser, acc.DestPassword, ct);
        if (!dstTest.Success)
        {
            acc.Status = MigrationStatus.Failed;
            acc.StatusMessage = $"Dest Error: {dstTest.Message}";
            return false;
        }

        acc.Status = MigrationStatus.Ready;
        acc.StatusMessage = "Verified (Ready)";
        return true;
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
            await TestSingleAccountAsync(source, dest, acc, token);
        });
    }

    public void Stop()
    {
        if (IsRunning && _cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _queue?.Writer.TryComplete();
            IsRunning = false;
        }
    }
}
