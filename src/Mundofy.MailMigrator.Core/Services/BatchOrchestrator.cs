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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeAccountCts = new();

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

        bool wasPaused = account.Status == MigrationStatus.Paused;
        bool wasFailed = account.Status == MigrationStatus.Failed;

        if (wasFailed)
        {
            Interlocked.Decrement(ref _failedAccounts);
        }

        if (!wasPaused && !wasFailed)
        {
            Interlocked.Increment(ref _totalAccounts);
        }

        account.Status = MigrationStatus.Queued;
        account.StatusMessage = "Queued in active batch...";
        account.TotalMessages = 0;
        account.CopiedMessages = 0;
        account.SkippedMessages = 0;
        account.FailedMessages = 0;
        account.BytesTransferred = 0;

        _queue.Writer.TryWrite(account);
        NotifyProgress();

        LogEmitted?.Invoke(this, new LogEntry
        {
            Level = LogLevel.Success,
            Message = wasPaused || wasFailed
                ? $"⚡ Resumed/Re-queued '{account.SourceUser}' into active running batch."
                : $"⚡ Dynamically added '{account.SourceUser}' to active running batch (batch total: {_totalAccounts})."
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
        _activeAccountCts.Clear();

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

                            using var accountCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            _activeAccountCts[account.Id] = accountCts;

                            try
                            {
                                await _migrationService.MigrateAccountAsync(source, dest, account, options, accountProgress, accountCts.Token);

                                if (account.Status == MigrationStatus.Completed)
                                    Interlocked.Increment(ref _completedAccounts);
                                else if (account.Status != MigrationStatus.Paused)
                                    Interlocked.Increment(ref _failedAccounts);

                                Interlocked.Add(ref _totalCopiedMessages, account.CopiedMessages);
                                Interlocked.Add(ref _totalFailedMessages, account.FailedMessages);
                                Interlocked.Add(ref _totalBytesTransferred, account.BytesTransferred);
                            }
                            finally
                            {
                                _activeAccountCts.TryRemove(account.Id, out _);
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
            _activeAccountCts.Clear();
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

        var (srcOk, srcMsg, srcQuota) = await _migrationService.TestConnectionWithQuotaAsync(source, acc.SourceUser, acc.SourcePassword, ct);
        if (!srcOk)
        {
            acc.Status = MigrationStatus.Failed;
            acc.StatusMessage = $"Source Error: {srcMsg}";
            return false;
        }
        acc.SourceQuota = srcQuota;

        acc.StatusMessage = "Testing destination connection...";
        var (dstOk, dstMsg, dstQuota) = await _migrationService.TestConnectionWithQuotaAsync(dest, acc.DestUser, acc.DestPassword, ct);
        if (!dstOk)
        {
            acc.Status = MigrationStatus.Failed;
            acc.StatusMessage = $"Dest Error: {dstMsg}";
            return false;
        }
        acc.DestQuota = dstQuota;

        if (dstQuota != null && srcQuota != null &&
            dstQuota.StorageAvailableBytes.HasValue && srcQuota.StorageUsedBytes.HasValue &&
            dstQuota.StorageAvailableBytes.Value < srcQuota.StorageUsedBytes.Value)
        {
            acc.Status = MigrationStatus.Ready;
            acc.StatusMessage = $"Verified (Warning: Dest {MailboxQuotaInfo.FormatBytes(dstQuota.StorageAvailableBytes.Value)} < Source {MailboxQuotaInfo.FormatBytes(srcQuota.StorageUsedBytes.Value)})";
            return true;
        }

        acc.Status = MigrationStatus.Ready;
        acc.StatusMessage = "Verified (Ready)";
        return true;
    }

    public async Task CheckAccountQuotasAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        AccountJob acc,
        CancellationToken ct = default)
    {
        acc.StatusMessage = "Checking quotas...";
        try
        {
            var srcQuota = await _migrationService.GetMailboxQuotaAsync(source, acc.SourceUser, acc.SourcePassword, ct);
            acc.SourceQuota = srcQuota;

            var dstQuota = await _migrationService.GetMailboxQuotaAsync(dest, acc.DestUser, acc.DestPassword, ct);
            acc.DestQuota = dstQuota;

            if (dstQuota != null && srcQuota != null &&
                dstQuota.StorageAvailableBytes.HasValue && srcQuota.StorageUsedBytes.HasValue &&
                dstQuota.StorageAvailableBytes.Value < srcQuota.StorageUsedBytes.Value)
            {
                acc.StatusMessage = $"Dest low ({MailboxQuotaInfo.FormatBytes(dstQuota.StorageAvailableBytes.Value)} free < {MailboxQuotaInfo.FormatBytes(srcQuota.StorageUsedBytes.Value)} needed)";
            }
            else
            {
                acc.StatusMessage = "Quotas checked";
            }
        }
        catch (Exception ex)
        {
            acc.StatusMessage = $"Quota check failed: {ex.Message}";
        }
    }

    public async Task CheckAllAccountQuotasAsync(
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
            await CheckAccountQuotasAsync(source, dest, acc, token);
        });
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

    /// <summary>
    /// Pauses an actively migrating account row without stopping the overall batch.
    /// </summary>
    public bool PauseAccount(AccountJob account)
    {
        if (_activeAccountCts.TryGetValue(account.Id, out var accountCts))
        {
            account.StatusMessage = "Pausing...";
            try
            {
                accountCts.Cancel();
            }
            catch (ObjectDisposedException) { }

            LogEmitted?.Invoke(this, new LogEntry
            {
                Level = LogLevel.Warning,
                Message = $"[{account.SourceUser}] Pausing account migration..."
            });
            return true;
        }
        return false;
    }
}
