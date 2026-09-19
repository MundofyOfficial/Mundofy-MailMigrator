using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mundofy.MailMigrator.Core;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Mundofy.MailMigrator.Core.Services;

namespace Mundofy.MailMigrator.Mac.ViewModels;

public class MacMainViewModel : INotifyPropertyChanged
{
    private readonly ImapMigrationService _migrationService;
    private readonly BatchOrchestrator _batchOrchestrator;
    private readonly ServerAutoDiscoveryService _autoDiscoveryService;
    private CancellationTokenSource? _singleCts;

    public MacMainViewModel()
    {
        _migrationService = new ImapMigrationService();
        _batchOrchestrator = new BatchOrchestrator(_migrationService);
        _autoDiscoveryService = new ServerAutoDiscoveryService();

        _migrationService.LogEmitted += OnLogEmitted;
        _batchOrchestrator.BatchProgressUpdated += OnBatchProgressUpdated;

        Logs = new ObservableCollection<LogEntry>();
        BatchAccounts = new ObservableCollection<AccountJob>();

        AddSampleBatchAccounts();

        AddLog(LogLevel.Info, $"Mundofy MailMigrator (macOS) v{AppVersion.Current} initialized.");

        // Non-blocking background version check
        _ = CheckForUpdateSilentlyAsync();
    }

    public string AppVersionString => AppVersion.Current;
    public string AppVersionDisplay => $"v{AppVersion.Current}";

    #region Single Migration Properties

    private string _singleSourceHost = "";
    public string SingleSourceHost
    {
        get => _singleSourceHost;
        set => SetField(ref _singleSourceHost, value);
    }

    private int _singleSourcePort = 993;
    public int SingleSourcePort
    {
        get => _singleSourcePort;
        set => SetField(ref _singleSourcePort, value);
    }

    private ServerProtocol _singleSourceProtocol = ServerProtocol.Imap;
    public ServerProtocol SingleSourceProtocol
    {
        get => _singleSourceProtocol;
        set
        {
            if (SetField(ref _singleSourceProtocol, value))
            {
                SingleSourcePort = value == ServerProtocol.Pop3 ? 995 : 993;
            }
        }
    }

    private bool _singleSourceUseSsl = true;
    public bool SingleSourceUseSsl
    {
        get => _singleSourceUseSsl;
        set => SetField(ref _singleSourceUseSsl, value);
    }

    private string _singleSourceUser = "";
    public string SingleSourceUser
    {
        get => _singleSourceUser;
        set => SetField(ref _singleSourceUser, value);
    }

    private string _singleSourcePassword = "";
    public string SingleSourcePassword
    {
        get => _singleSourcePassword;
        set => SetField(ref _singleSourcePassword, value);
    }

    private string _singleSourceQuotaText = "Quota: Ready to check";
    public string SingleSourceQuotaText
    {
        get => _singleSourceQuotaText;
        set => SetField(ref _singleSourceQuotaText, value);
    }

    private string _singleDestHost = "";
    public string SingleDestHost
    {
        get => _singleDestHost;
        set => SetField(ref _singleDestHost, value);
    }

    private int _singleDestPort = 993;
    public int SingleDestPort
    {
        get => _singleDestPort;
        set => SetField(ref _singleDestPort, value);
    }

    private ServerProtocol _singleDestProtocol = ServerProtocol.Imap;
    public ServerProtocol SingleDestProtocol
    {
        get => _singleDestProtocol;
        set => SetField(ref _singleDestProtocol, value);
    }

    private bool _singleDestUseSsl = true;
    public bool SingleDestUseSsl
    {
        get => _singleDestUseSsl;
        set => SetField(ref _singleDestUseSsl, value);
    }

    private string _singleDestUser = "";
    public string SingleDestUser
    {
        get => _singleDestUser;
        set => SetField(ref _singleDestUser, value);
    }

    private string _singleDestPassword = "";
    public string SingleDestPassword
    {
        get => _singleDestPassword;
        set => SetField(ref _singleDestPassword, value);
    }

    private string _singleDestQuotaText = "Quota: Ready to check";
    public string SingleDestQuotaText
    {
        get => _singleDestQuotaText;
        set => SetField(ref _singleDestQuotaText, value);
    }

    private bool _isSingleMigrating;
    public bool IsSingleMigrating
    {
        get => _isSingleMigrating;
        set
        {
            if (SetField(ref _isSingleMigrating, value))
            {
                OnPropertyChanged(nameof(CanStartSingleMigration));
                OnPropertyChanged(nameof(CanModifyControls));
            }
        }
    }

    public bool CanStartSingleMigration => !IsSingleMigrating && !IsBatchMigrating;
    public bool CanModifyControls => !IsSingleMigrating && !IsBatchMigrating;

    private double _singleProgressPercent;
    public double SingleProgressPercent
    {
        get => _singleProgressPercent;
        set => SetField(ref _singleProgressPercent, value);
    }

    private string _singleStatusText = "Ready";
    public string SingleStatusText
    {
        get => _singleStatusText;
        set => SetField(ref _singleStatusText, value);
    }

    private string _singleFolderProgressText = "";
    public string SingleFolderProgressText
    {
        get => _singleFolderProgressText;
        set => SetField(ref _singleFolderProgressText, value);
    }

    private string _singleSpeedText = "0.0 MB/s";
    public string SingleSpeedText
    {
        get => _singleSpeedText;
        set => SetField(ref _singleSpeedText, value);
    }

    #endregion

    #region Batch Migration Properties

    private string _batchSourceHost = "";
    public string BatchSourceHost
    {
        get => _batchSourceHost;
        set => SetField(ref _batchSourceHost, value);
    }

    private int _batchSourcePort = 993;
    public int BatchSourcePort
    {
        get => _batchSourcePort;
        set => SetField(ref _batchSourcePort, value);
    }

    private ServerProtocol _batchSourceProtocol = ServerProtocol.Imap;
    public ServerProtocol BatchSourceProtocol
    {
        get => _batchSourceProtocol;
        set => SetField(ref _batchSourceProtocol, value);
    }

    private bool _batchSourceUseSsl = true;
    public bool BatchSourceUseSsl
    {
        get => _batchSourceUseSsl;
        set => SetField(ref _batchSourceUseSsl, value);
    }

    private string _batchDestHost = "";
    public string BatchDestHost
    {
        get => _batchDestHost;
        set => SetField(ref _batchDestHost, value);
    }

    private int _batchDestPort = 993;
    public int BatchDestPort
    {
        get => _batchDestPort;
        set => SetField(ref _batchDestPort, value);
    }

    private ServerProtocol _batchDestProtocol = ServerProtocol.Imap;
    public ServerProtocol BatchDestProtocol
    {
        get => _batchDestProtocol;
        set => SetField(ref _batchDestProtocol, value);
    }

    private bool _batchDestUseSsl = true;
    public bool BatchDestUseSsl
    {
        get => _batchDestUseSsl;
        set => SetField(ref _batchDestUseSsl, value);
    }

    private int _concurrency = 4;
    public int Concurrency
    {
        get => _concurrency;
        set => SetField(ref _concurrency, Math.Clamp(value, 1, 16));
    }

    public ObservableCollection<AccountJob> BatchAccounts { get; }

    private AccountJob? _selectedAccount;
    public AccountJob? SelectedAccount
    {
        get => _selectedAccount;
        set => SetField(ref _selectedAccount, value);
    }

    private bool _isBatchMigrating;
    public bool IsBatchMigrating
    {
        get => _isBatchMigrating;
        set
        {
            if (SetField(ref _isBatchMigrating, value))
            {
                OnPropertyChanged(nameof(CanStartBatchMigration));
                OnPropertyChanged(nameof(CanModifyControls));
            }
        }
    }

    public bool CanStartBatchMigration => !IsBatchMigrating && !IsSingleMigrating;

    private double _batchProgressPercent;
    public double BatchProgressPercent
    {
        get => _batchProgressPercent;
        set => SetField(ref _batchProgressPercent, value);
    }

    private string _batchStatusText = "Ready";
    public string BatchStatusText
    {
        get => _batchStatusText;
        set => SetField(ref _batchStatusText, value);
    }

    #endregion

    #region Settings & Options

    private bool _deduplicate = true;
    public bool Deduplicate
    {
        get => _deduplicate;
        set => SetField(ref _deduplicate, value);
    }

    private bool _allowInvalidCertificates;
    public bool AllowInvalidCertificates
    {
        get => _allowInvalidCertificates;
        set => SetField(ref _allowInvalidCertificates, value);
    }

    private string _dstRootFolder = "";
    public string DstRootFolder
    {
        get => _dstRootFolder;
        set => SetField(ref _dstRootFolder, value);
    }

    private string _skipFolders = "Trash, Junk, Spam, Deleted Items";
    public string SkipFolders
    {
        get => _skipFolders;
        set => SetField(ref _skipFolders, value);
    }

    private string _denyFlags = @"\Recent";
    public string DenyFlags
    {
        get => _denyFlags;
        set => SetField(ref _denyFlags, value);
    }

    #endregion

    #region Activity Logs

    public ObservableCollection<LogEntry> Logs { get; }

    private string _logSearchText = "";
    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (SetField(ref _logSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredLogs));
                OnPropertyChanged(nameof(LogCountStatusText));
            }
        }
    }

    public IEnumerable<LogEntry> FilteredLogs
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogSearchText)) return Logs;
            return Logs.Where(l => l.Message.Contains(LogSearchText, StringComparison.OrdinalIgnoreCase)
                                || l.Level.ToString().Contains(LogSearchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string LogCountStatusText => $"{FilteredLogs.Count()} of {Logs.Count} records";

    public void AddLog(LogLevel level, string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var entry = new LogEntry { Level = level, Message = message };
            Logs.Add(entry);
            if (Logs.Count > 3000) Logs.RemoveAt(0);
            OnPropertyChanged(nameof(FilteredLogs));
            OnPropertyChanged(nameof(LogCountStatusText));
        });
    }

    public void ClearLogs()
    {
        Logs.Clear();
        OnPropertyChanged(nameof(FilteredLogs));
        OnPropertyChanged(nameof(LogCountStatusText));
    }

    #endregion

    #region Update Info

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetField(ref _isUpdateAvailable, value);
    }

    private string _latestVersion = "";
    public string LatestVersion
    {
        get => _latestVersion;
        set => SetField(ref _latestVersion, value);
    }

    private async Task CheckForUpdateSilentlyAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mundofy-MailMigrator-Mac", AppVersion.Current));
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetStringAsync("https://api.github.com/repos/MundofyOfficial/Mundofy-MailMigrator/releases/latest");
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElem))
            {
                var tag = tagElem.GetString() ?? "";
                var latest = tag.TrimStart('v', 'V').Trim();
                if (Version.TryParse(latest, out var latestVer) && Version.TryParse(AppVersion.Current, out var curVer))
                {
                    if (latestVer > curVer)
                    {
                        LatestVersion = latest;
                        IsUpdateAvailable = true;
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback in air-gapped / offline environments
        }
    }

    #endregion

    #region Migration Operations

    public async Task StartSingleMigrationAsync()
    {
        if (IsSingleMigrating) return;

        IsSingleMigrating = true;
        SingleProgressPercent = 0;
        SingleStatusText = "Connecting to servers...";
        _singleCts = new CancellationTokenSource();

        var srcEndpoint = new ServerEndpoint
        {
            Host = SingleSourceHost,
            Port = SingleSourcePort,
            Protocol = SingleSourceProtocol,
            UseSsl = SingleSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var dstEndpoint = new ServerEndpoint
        {
            Host = SingleDestHost,
            Port = SingleDestPort,
            Protocol = SingleDestProtocol,
            UseSsl = SingleDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var options = new MigrationOptions
        {
            Deduplicate = Deduplicate,
            DstRootFolder = DstRootFolder,
            SkipFolders = SkipFolders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            DenyFlags = DenyFlags.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
        };

        var job = new AccountJob
        {
            SourceUser = SingleSourceUser,
            SourcePassword = SingleSourcePassword,
            DestUser = SingleDestUser,
            DestPassword = SingleDestPassword
        };

        try
        {
            AddLog(LogLevel.Info, $"Starting single migration: {SingleSourceUser} -> {SingleDestUser}");
            var progress = new Progress<AccountJob>(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SingleProgressPercent = job.ProgressPercentage;
                    SingleStatusText = job.StatusMessage;
                });
            });

            await _migrationService.MigrateAccountAsync(srcEndpoint, dstEndpoint, job, options, progress, _singleCts.Token);

            SingleProgressPercent = job.ProgressPercentage;
            SingleStatusText = job.Status == MigrationStatus.Completed ? "Migration Completed Successfully!" : $"Status: {job.StatusMessage}";
            AddLog(job.Status == MigrationStatus.Completed ? LogLevel.Success : LogLevel.Error, SingleStatusText);
        }
        catch (OperationCanceledException)
        {
            SingleStatusText = "Migration Canceled by User";
            AddLog(LogLevel.Warning, SingleStatusText);
        }
        catch (Exception ex)
        {
            SingleStatusText = $"Error: {ex.Message}";
            AddLog(LogLevel.Error, SingleStatusText);
        }
        finally
        {
            IsSingleMigrating = false;
        }
    }

    public void CancelSingleMigration()
    {
        _singleCts?.Cancel();
    }

    public async Task StartBatchMigrationAsync()
    {
        if (IsBatchMigrating || BatchAccounts.Count == 0) return;

        IsBatchMigrating = true;
        BatchProgressPercent = 0;
        BatchStatusText = "Starting batch migration...";

        var defaultSrc = new ServerEndpoint
        {
            Host = BatchSourceHost,
            Port = BatchSourcePort,
            Protocol = BatchSourceProtocol,
            UseSsl = BatchSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var defaultDst = new ServerEndpoint
        {
            Host = BatchDestHost,
            Port = BatchDestPort,
            Protocol = BatchDestProtocol,
            UseSsl = BatchDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var options = new MigrationOptions
        {
            MaxConcurrency = Concurrency,
            Deduplicate = Deduplicate,
            DstRootFolder = DstRootFolder,
            SkipFolders = SkipFolders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            DenyFlags = DenyFlags.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
        };

        // Reset completed accounts for clean run
        foreach (var acc in BatchAccounts)
        {
            acc.ResetForMigration();
        }

        try
        {
            AddLog(LogLevel.Info, $"Initiating batch migration for {BatchAccounts.Count} accounts (Concurrency: {Concurrency})...");
            var batchProgress = new Progress<AccountJob>(_ => { });
            await _batchOrchestrator.RunBatchAsync(defaultSrc, defaultDst, BatchAccounts, options, batchProgress);
            BatchStatusText = "Batch Migration Finished";
            AddLog(LogLevel.Success, BatchStatusText);
        }
        catch (Exception ex)
        {
            BatchStatusText = $"Batch Failed: {ex.Message}";
            AddLog(LogLevel.Error, BatchStatusText);
        }
        finally
        {
            IsBatchMigrating = false;
        }
    }

    public void CancelBatchMigration()
    {
        _batchOrchestrator.Stop();
    }

    public async Task CheckSingleSourceQuotaAsync()
    {
        SingleSourceQuotaText = "Checking source quota...";
        var ep = new ServerEndpoint
        {
            Host = SingleSourceHost,
            Port = SingleSourcePort,
            Protocol = SingleSourceProtocol,
            UseSsl = SingleSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var quota = await _migrationService.GetMailboxQuotaAsync(ep, SingleSourceUser, SingleSourcePassword);
        SingleSourceQuotaText = quota != null && quota.StorageUsedBytes.HasValue ? quota.FormattedSummary : "Storage: Unlimited / Unmetered";
    }

    public async Task CheckSingleDestQuotaAsync()
    {
        SingleDestQuotaText = "Checking destination quota...";
        var ep = new ServerEndpoint
        {
            Host = SingleDestHost,
            Port = SingleDestPort,
            Protocol = SingleDestProtocol,
            UseSsl = SingleDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var quota = await _migrationService.GetMailboxQuotaAsync(ep, SingleDestUser, SingleDestPassword);
        SingleDestQuotaText = quota != null && quota.StorageUsedBytes.HasValue ? quota.FormattedSummary : "Storage: Unlimited / Unmetered";
    }

    public async Task AutoDetectSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleSourceUser)) return;
        AddLog(LogLevel.Info, $"Auto-detecting server profile for {SingleSourceUser}...");
        var result = await _autoDiscoveryService.DiscoverAsync(SingleSourceUser);
        if (result != null && result.Success)
        {
            SingleSourceHost = result.Host;
            SingleSourcePort = result.Port;
            SingleSourceUseSsl = result.UseSsl;
            AddLog(LogLevel.Success, $"Discovered {result.Host}:{result.Port} (SSL: {result.UseSsl}) via {result.DetectionSource}");
        }
        else
        {
            AddLog(LogLevel.Warning, "Could not auto-detect source host. Please enter manually.");
        }
    }

    public async Task AutoDetectDestAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleDestUser)) return;
        AddLog(LogLevel.Info, $"Auto-detecting destination server profile for {SingleDestUser}...");
        var result = await _autoDiscoveryService.DiscoverAsync(SingleDestUser);
        if (result != null && result.Success)
        {
            SingleDestHost = result.Host;
            SingleDestPort = result.Port;
            SingleDestUseSsl = result.UseSsl;
            AddLog(LogLevel.Success, $"Discovered {result.Host}:{result.Port} (SSL: {result.UseSsl}) via {result.DetectionSource}");
        }
        else
        {
            AddLog(LogLevel.Warning, "Could not auto-detect destination host. Please enter manually.");
        }
    }

    public void AddBatchAccount()
    {
        BatchAccounts.Add(new AccountJob
        {
            SourceUser = "user@source.com",
            SourcePassword = "",
            DestUser = "user@dest.com",
            DestPassword = "",
            Status = MigrationStatus.Ready
        });
    }

    public void RemoveSelectedAccount()
    {
        if (SelectedAccount != null)
        {
            BatchAccounts.Remove(SelectedAccount);
        }
    }

    private void AddSampleBatchAccounts()
    {
        BatchAccounts.Add(new AccountJob
        {
            SourceUser = "sales@company.com",
            SourcePassword = "",
            DestUser = "sales@newhost.com",
            DestPassword = "",
            Status = MigrationStatus.Ready
        });
        BatchAccounts.Add(new AccountJob
        {
            SourceUser = "info@company.com",
            SourcePassword = "",
            DestUser = "info@newhost.com",
            DestPassword = "",
            Status = MigrationStatus.Ready
        });
    }

    #endregion

    #region Event Callbacks

    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        AddLog(entry.Level, entry.Message);
    }

    private void OnBatchProgressUpdated(object? sender, BatchProgressReport e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BatchProgressPercent = e.OverallPercentage;
            BatchStatusText = $"Progress: {e.CompletedAccounts}/{e.TotalAccounts} completed ({e.ActiveWorkers} active)";
        });
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}