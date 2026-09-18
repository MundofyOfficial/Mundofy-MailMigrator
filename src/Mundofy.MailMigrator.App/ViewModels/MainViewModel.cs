using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using System.Globalization;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Mundofy.MailMigrator.Core.Services;
using Mundofy.MailMigrator.App.Dialogs;
using Mundofy.MailMigrator.App.Services;

namespace Mundofy.MailMigrator.App.ViewModels;

public enum BatchImportDecision
{
    Cancel,
    Replace,
    Append
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ImapMigrationService _migrationService;
    private readonly BatchOrchestrator _batchOrchestrator;
    private readonly ServerAutoDiscoveryService _autoDiscoveryService;
    private CancellationTokenSource? _singleCts;

    public MainViewModel()
    {
        _migrationService = new ImapMigrationService();
        _batchOrchestrator = new BatchOrchestrator(_migrationService);
        _autoDiscoveryService = new ServerAutoDiscoveryService();

        _migrationService.LogEmitted += OnLogEmitted;
        _batchOrchestrator.BatchProgressUpdated += OnBatchProgressUpdated;

        // Initialize Commands
        TestSingleSourceCommand = new RelayCommand(async () => await TestSingleSourceAsync(), () => !IsSingleMigrating);
        TestSingleDestCommand = new RelayCommand(async () => await TestSingleDestAsync(), () => !IsSingleMigrating);
        StartSingleMigrationCommand = new RelayCommand(async () => await StartSingleMigrationAsync(), () => !IsSingleMigrating);
        CancelSingleMigrationCommand = new RelayCommand(() => CancelSingleMigration(), () => IsSingleMigrating);

        AutoDetectSingleSourceCommand = new RelayCommand(async () => await AutoDetectSingleSourceAsync(), () => !IsSingleMigrating && !IsDetectingSource);
        AutoDetectSingleDestCommand = new RelayCommand(async () => await AutoDetectSingleDestAsync(), () => !IsSingleMigrating && !IsDetectingDest);
        AutoDetectBatchSourceCommand = new RelayCommand(async () => await AutoDetectBatchSourceAsync(), () => !IsBatchRunning && !IsDetectingBatchSource);
        AutoDetectBatchDestCommand = new RelayCommand(async () => await AutoDetectBatchDestAsync(), () => !IsBatchRunning && !IsDetectingBatchDest);

        AddAccountCommand = new RelayCommand(AddAccount);
        RemoveAccountCommand = new RelayCommand(RemoveAccount, () => SelectedAccount != null && SelectedAccount.Status != MigrationStatus.InProgress);
        DeleteAccountRowCommand = new RelayCommand(param =>
        {
            var job = param as AccountJob ?? SelectedAccount;
            if (job != null)
            {
                if (job.Status == MigrationStatus.InProgress)
                {
                    DarkMessageBox.Show("Cannot delete an account while it is actively migrating.", "Account Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                BatchAccounts.Remove(job);
            }
        });
        StartSingleAccountInBatchCommand = new RelayCommand(param =>
        {
            var job = param as AccountJob ?? SelectedAccount;
            if (job != null) StartOrQueueAccount(job);
        });
        QueueSelectedAccountCommand = new RelayCommand(() =>
        {
            if (SelectedAccount != null) StartOrQueueAccount(SelectedAccount);
        }, () => SelectedAccount != null && SelectedAccount.Status != MigrationStatus.InProgress);
        TestSelectedAccountCommand = new RelayCommand(async () => await TestSelectedAccountAsync(), () => SelectedAccount != null && !IsBatchRunning);
        CopySourceEmailCommand = new RelayCommand(CopySourceEmail, () => SelectedAccount != null);
        CopyDestEmailCommand = new RelayCommand(CopyDestEmail, () => SelectedAccount != null);
        ClearCompletedAccountsCommand = new RelayCommand(ClearCompletedAccounts, () => !IsBatchRunning && BatchAccounts.Any(a => a.Status == MigrationStatus.Completed));

        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard);
        LoadCfgCommand = new RelayCommand(LoadCfgFile, () => !IsBatchRunning);
        ImportCsvCommand = new RelayCommand(ImportCsvFile);
        ExportCsvCommand = new RelayCommand(ExportCsvFile, () => BatchAccounts.Count > 0);
        TestAllBatchCommand = new RelayCommand(async () => await TestAllBatchAsync(), () => !IsBatchRunning && BatchAccounts.Count > 0);
        StartBatchCommand = new RelayCommand(async () => await StartBatchAsync(), () => !IsBatchRunning && BatchAccounts.Count > 0);
        StopBatchCommand = new RelayCommand(StopBatch, () => IsBatchRunning);
        ClearBatchCommand = new RelayCommand(ClearBatch, () => !IsBatchRunning);
        ClearLogsCommand = new RelayCommand(ClearLogs);

        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesExplicitAsync());
        OpenUpdateDialogCommand = new RelayCommand(() =>
        {
            if (UpdateInfo != null)
                UpdateDialog.ShowDialog(Application.Current?.MainWindow, UpdateInfo);
        });

        TogglePasswordMaskCommand = new RelayCommand(() =>
        {
            MaskPasswords = !MaskPasswords;
            SaveCurrentSettings();
        });

        // Load persisted user settings
        LoadSavedSettings();

        // Populate sample initial row in batch table
        BatchAccounts.Add(new AccountJob
        {
            SourceUser = "user1@source.com",
            SourcePassword = "password",
            DestUser = "user1@dest.com",
            DestPassword = "password",
            Status = MigrationStatus.Ready,
            StatusMessage = "Ready"
        });

        AddLog(LogLevel.Info, "Mundofy MailMigrator v1.2.1 initialized.");

        // Non-blocking background check for updates on startup
        _ = CheckForUpdatesSilentlyAsync();
    }

    #region Single Migration Properties

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
        set
        {
            if (SetField(ref _singleSourceUser, value))
            {
                if (string.IsNullOrWhiteSpace(SingleSourceHost) && value.Contains('@') && value.IndexOf('.', value.IndexOf('@')) > 0)
                {
                    _ = AutoDetectSingleSourceAsync();
                }
            }
        }
    }

    private string _singleSourcePassword = "";
    public string SingleSourcePassword
    {
        get => _singleSourcePassword;
        set => SetField(ref _singleSourcePassword, value);
    }

    private string _singleSourceStatus = "Not Tested";
    public string SingleSourceStatus
    {
        get => _singleSourceStatus;
        set => SetField(ref _singleSourceStatus, value);
    }

    private bool _isDetectingSource;
    public bool IsDetectingSource
    {
        get => _isDetectingSource;
        set
        {
            if (SetField(ref _isDetectingSource, value))
            {
                AutoDetectSingleSourceCommand?.RaiseCanExecuteChanged();
            }
        }
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
        set
        {
            if (SetField(ref _singleDestUser, value))
            {
                if (string.IsNullOrWhiteSpace(SingleDestHost) && value.Contains('@') && value.IndexOf('.', value.IndexOf('@')) > 0)
                {
                    _ = AutoDetectSingleDestAsync();
                }
            }
        }
    }

    private string _singleDestPassword = "";
    public string SingleDestPassword
    {
        get => _singleDestPassword;
        set => SetField(ref _singleDestPassword, value);
    }

    private string _singleDestStatus = "Not Tested";
    public string SingleDestStatus
    {
        get => _singleDestStatus;
        set => SetField(ref _singleDestStatus, value);
    }

    private bool _isDetectingDest;
    public bool IsDetectingDest
    {
        get => _isDetectingDest;
        set
        {
            if (SetField(ref _isDetectingDest, value))
            {
                AutoDetectSingleDestCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isDetectingBatchSource;
    public bool IsDetectingBatchSource
    {
        get => _isDetectingBatchSource;
        set
        {
            if (SetField(ref _isDetectingBatchSource, value))
            {
                AutoDetectBatchSourceCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isDetectingBatchDest;
    public bool IsDetectingBatchDest
    {
        get => _isDetectingBatchDest;
        set
        {
            if (SetField(ref _isDetectingBatchDest, value))
            {
                AutoDetectBatchDestCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private AccountJob _singleJob = new();
    public AccountJob SingleJob
    {
        get => _singleJob;
        set => SetField(ref _singleJob, value);
    }

    private bool _isSingleMigrating;
    public bool IsSingleMigrating
    {
        get => _isSingleMigrating;
        set => SetField(ref _isSingleMigrating, value);
    }

    #endregion

    #region Batch Migration Properties

    private ServerProtocol _batchSourceProtocol = ServerProtocol.Imap;
    public ServerProtocol BatchSourceProtocol
    {
        get => _batchSourceProtocol;
        set
        {
            if (SetField(ref _batchSourceProtocol, value))
            {
                BatchSourcePort = value == ServerProtocol.Pop3 ? 995 : 993;
            }
        }
    }

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

    public ObservableCollection<AccountJob> BatchAccounts { get; } = new();

    private AccountJob? _selectedAccount;
    public AccountJob? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetField(ref _selectedAccount, value))
            {
                RemoveAccountCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isBatchRunning;
    public bool IsBatchRunning
    {
        get => _isBatchRunning;
        set => SetField(ref _isBatchRunning, value);
    }

    private double _batchOverallProgress;
    public double BatchOverallProgress
    {
        get => _batchOverallProgress;
        set => SetField(ref _batchOverallProgress, value);
    }

    private string _batchStatusText = "Ready to start";
    public string BatchStatusText
    {
        get => _batchStatusText;
        set => SetField(ref _batchStatusText, value);
    }

    private int _activeWorkersCount;
    public int ActiveWorkersCount
    {
        get => _activeWorkersCount;
        set => SetField(ref _activeWorkersCount, value);
    }

    private bool _maskPasswords = true;
    public bool MaskPasswords
    {
        get => _maskPasswords;
        set => SetField(ref _maskPasswords, value);
    }

    #endregion

    #region General Options

    private bool _allowInvalidCertificates = false;
    public bool AllowInvalidCertificates
    {
        get => _allowInvalidCertificates;
        set => SetField(ref _allowInvalidCertificates, value);
    }

    private bool _deduplicate = true;
    public bool Deduplicate
    {
        get => _deduplicate;
        set => SetField(ref _deduplicate, value);
    }

    private string _skipFolders = "Trash, Junk, Spam, Deleted Items";
    public string SkipFolders
    {
        get => _skipFolders;
        set => SetField(ref _skipFolders, value);
    }

    private string _denyFlags = "\\Recent";
    public string DenyFlags
    {
        get => _denyFlags;
        set => SetField(ref _denyFlags, value);
    }

    private string _dstRootFolder = "";
    public string DstRootFolder
    {
        get => _dstRootFolder;
        set => SetField(ref _dstRootFolder, value);
    }

    #endregion

    #region Date Filtering (DD-MM-YYYY)

    private bool _enableDateFilter = false;
    public bool EnableDateFilter
    {
        get => _enableDateFilter;
        set
        {
            if (SetField(ref _enableDateFilter, value))
            {
                ValidateDateFilter();
            }
        }
    }

    private string _sinceDateText = "";
    public string SinceDateText
    {
        get => _sinceDateText;
        set
        {
            if (SetField(ref _sinceDateText, value))
            {
                ValidateDateFilter();
            }
        }
    }

    private string _beforeDateText = "";
    public string BeforeDateText
    {
        get => _beforeDateText;
        set
        {
            if (SetField(ref _beforeDateText, value))
            {
                ValidateDateFilter();
            }
        }
    }

    private string _selectedDatePreset = "All Emails (No date limit)";
    public string SelectedDatePreset
    {
        get => _selectedDatePreset;
        set
        {
            if (SetField(ref _selectedDatePreset, value))
            {
                ApplyDatePreset(value);
            }
        }
    }

    public List<string> DatePresets { get; } = new()
    {
        "All Emails (No date limit)",
        "Last 6 Months",
        "Last 1 Year",
        "Last 2 Years",
        "Custom Date Range"
    };

    private string _dateFilterValidationMessage = "";
    public string DateFilterValidationMessage
    {
        get => _dateFilterValidationMessage;
        set => SetField(ref _dateFilterValidationMessage, value);
    }

    private void ApplyDatePreset(string preset)
    {
        if (preset.StartsWith("All Emails", StringComparison.OrdinalIgnoreCase))
        {
            EnableDateFilter = false;
            SinceDateText = "";
            BeforeDateText = "";
        }
        else if (preset.StartsWith("Last 6 Months", StringComparison.OrdinalIgnoreCase))
        {
            EnableDateFilter = true;
            SinceDateText = DateTime.Today.AddMonths(-6).ToString("dd-MM-yyyy");
            BeforeDateText = "";
        }
        else if (preset.StartsWith("Last 1 Year", StringComparison.OrdinalIgnoreCase))
        {
            EnableDateFilter = true;
            SinceDateText = DateTime.Today.AddYears(-1).ToString("dd-MM-yyyy");
            BeforeDateText = "";
        }
        else if (preset.StartsWith("Last 2 Years", StringComparison.OrdinalIgnoreCase))
        {
            EnableDateFilter = true;
            SinceDateText = DateTime.Today.AddYears(-2).ToString("dd-MM-yyyy");
            BeforeDateText = "";
        }
        else if (preset.StartsWith("Custom", StringComparison.OrdinalIgnoreCase))
        {
            EnableDateFilter = true;
        }
    }

    private void ValidateDateFilter()
    {
        if (!EnableDateFilter)
        {
            DateFilterValidationMessage = "";
            return;
        }

        bool sinceValid = true;
        DateTime? since = null;
        if (!string.IsNullOrWhiteSpace(SinceDateText))
        {
            if (TryParseDate(SinceDateText, out var dt))
                since = dt;
            else
                sinceValid = false;
        }

        bool beforeValid = true;
        DateTime? before = null;
        if (!string.IsNullOrWhiteSpace(BeforeDateText))
        {
            if (TryParseDate(BeforeDateText, out var dt))
                before = dt;
            else
                beforeValid = false;
        }

        if (!sinceValid && !beforeValid)
        {
            DateFilterValidationMessage = "⚠️ Invalid start and end dates. Use DD-MM-YYYY format (e.g. 01-01-2025).";
        }
        else if (!sinceValid)
        {
            DateFilterValidationMessage = "⚠️ Invalid start date. Use DD-MM-YYYY format (e.g. 01-01-2025).";
        }
        else if (!beforeValid)
        {
            DateFilterValidationMessage = "⚠️ Invalid end date. Use DD-MM-YYYY format (e.g. 01-01-2025).";
        }
        else if (since.HasValue && before.HasValue && since.Value > before.Value)
        {
            DateFilterValidationMessage = "⚠️ Start date cannot be after end date.";
        }
        else if (since.HasValue && before.HasValue)
        {
            DateFilterValidationMessage = $"✓ Migrating emails between {since:dd-MM-yyyy} and {before:dd-MM-yyyy}.";
        }
        else if (since.HasValue)
        {
            DateFilterValidationMessage = $"✓ Migrating emails received on or after {since:dd-MM-yyyy}.";
        }
        else if (before.HasValue)
        {
            DateFilterValidationMessage = $"✓ Migrating emails received on or before {before:dd-MM-yyyy}.";
        }
        else
        {
            DateFilterValidationMessage = "ℹ Enter a Start Date (DD-MM-YYYY) to filter emails.";
        }
    }

    public static bool TryParseDate(string input, out DateTime dt)
    {
        string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(input.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    #endregion

    #region Software Updates

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

    private UpdateInfo? _updateInfo;
    public UpdateInfo? UpdateInfo
    {
        get => _updateInfo;
        set => SetField(ref _updateInfo, value);
    }

    public RelayCommand CheckForUpdatesCommand { get; }
    public RelayCommand OpenUpdateDialogCommand { get; }

    private async Task CheckForUpdatesSilentlyAsync()
    {
        try
        {
            var info = await UpdateService.CheckForUpdateAsync();
            if (info.HasUpdate)
            {
                UpdateInfo = info;
                LatestVersion = info.LatestVersion;
                IsUpdateAvailable = true;
                AddLog(LogLevel.Info, $"⚡ Software update v{info.LatestVersion} is available on GitHub (current: v{info.CurrentVersion}).");
            }
        }
        catch { }
    }

    private async Task CheckForUpdatesExplicitAsync()
    {
        AddLog(LogLevel.Info, "Checking GitHub for latest release...");
        var info = await UpdateService.CheckForUpdateAsync();
        UpdateInfo = info;
        if (info.HasUpdate)
        {
            LatestVersion = info.LatestVersion;
            IsUpdateAvailable = true;
            UpdateDialog.ShowDialog(Application.Current?.MainWindow, info);
        }
        else
        {
            DarkMessageBox.Show($"You are running the latest version of Mundofy MailMigrator (v{info.CurrentVersion}).", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region Logs

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public void AddLog(LogLevel level, string message)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (Logs.Count > 1000) Logs.RemoveAt(0);
            Logs.Add(new LogEntry { Level = level, Message = message });
        });
    }

    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        AddLog(entry.Level, entry.Message);
    }

    private void ClearLogs() => Logs.Clear();

    #endregion

    #region Commands

    public RelayCommand TestSingleSourceCommand { get; }
    public RelayCommand TestSingleDestCommand { get; }
    public RelayCommand StartSingleMigrationCommand { get; }
    public RelayCommand CancelSingleMigrationCommand { get; }

    public RelayCommand AutoDetectSingleSourceCommand { get; }
    public RelayCommand AutoDetectSingleDestCommand { get; }
    public RelayCommand AutoDetectBatchSourceCommand { get; }
    public RelayCommand AutoDetectBatchDestCommand { get; }

    public RelayCommand AddAccountCommand { get; }
    public RelayCommand RemoveAccountCommand { get; }
    public RelayCommand DeleteAccountRowCommand { get; }
    public RelayCommand StartSingleAccountInBatchCommand { get; }
    public RelayCommand QueueSelectedAccountCommand { get; }
    public RelayCommand TestSelectedAccountCommand { get; }
    public RelayCommand CopySourceEmailCommand { get; }
    public RelayCommand CopyDestEmailCommand { get; }
    public RelayCommand ClearCompletedAccountsCommand { get; }
    public RelayCommand PasteFromClipboardCommand { get; }
    public RelayCommand LoadCfgCommand { get; }
    public RelayCommand ImportCsvCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand TestAllBatchCommand { get; }
    public RelayCommand StartBatchCommand { get; }
    public RelayCommand StopBatchCommand { get; }
    public RelayCommand ClearBatchCommand { get; }
    public RelayCommand ClearLogsCommand { get; }
    public RelayCommand TogglePasswordMaskCommand { get; }

    #endregion

    #region Action Methods

    private async Task AutoDetectSingleSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleSourceUser))
        {
            SingleSourceStatus = "Enter email address first";
            AddLog(LogLevel.Warning, "Auto-Detect Source: Please enter a username/email address first.");
            return;
        }

        try
        {
            IsDetectingSource = true;
            SingleSourceStatus = "⚡ Auto-detecting server...";
            AddLog(LogLevel.Info, $"⚡ Probing auto-discovery for '{SingleSourceUser}'...");

            var result = await _autoDiscoveryService.DiscoverAsync(SingleSourceUser);
            if (result.Success)
            {
                SingleSourceHost = result.Host;
                SingleSourcePort = result.Port;
                SingleSourceUseSsl = result.UseSsl;
                SingleSourceStatus = $"⚡ Discovered: {result.Host}";
                AddLog(LogLevel.Success, $"⚡ Auto-Discovery succeeded: {result.Host}:{result.Port} (SSL: {result.UseSsl}) via {result.DetectionSource}");
            }
            else
            {
                SingleSourceStatus = "Could not detect server";
                AddLog(LogLevel.Warning, $"Auto-discovery failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            SingleSourceStatus = "Auto-detect error";
            AddLog(LogLevel.Error, $"Auto-discovery error: {ex.Message}");
        }
        finally
        {
            IsDetectingSource = false;
        }
    }

    private async Task AutoDetectSingleDestAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleDestUser))
        {
            SingleDestStatus = "Enter email address first";
            AddLog(LogLevel.Warning, "Auto-Detect Destination: Please enter a username/email address first.");
            return;
        }

        try
        {
            IsDetectingDest = true;
            SingleDestStatus = "⚡ Auto-detecting server...";
            AddLog(LogLevel.Info, $"⚡ Probing auto-discovery for '{SingleDestUser}'...");

            var result = await _autoDiscoveryService.DiscoverAsync(SingleDestUser);
            if (result.Success)
            {
                SingleDestHost = result.Host;
                SingleDestPort = result.Port;
                SingleDestUseSsl = result.UseSsl;
                SingleDestStatus = $"⚡ Discovered: {result.Host}";
                AddLog(LogLevel.Success, $"⚡ Auto-Discovery succeeded: {result.Host}:{result.Port} (SSL: {result.UseSsl}) via {result.DetectionSource}");
            }
            else
            {
                SingleDestStatus = "Could not detect server";
                AddLog(LogLevel.Warning, $"Auto-discovery failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            SingleDestStatus = "Auto-detect error";
            AddLog(LogLevel.Error, $"Auto-discovery error: {ex.Message}");
        }
        finally
        {
            IsDetectingDest = false;
        }
    }

    private async Task AutoDetectBatchSourceAsync()
    {
        string query = BatchSourceHost;
        if (string.IsNullOrWhiteSpace(query) && BatchAccounts.Count > 0)
        {
            query = BatchAccounts[0].SourceUser;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            AddLog(LogLevel.Warning, "Auto-Detect Batch Source: Please enter a domain or add an account first.");
            return;
        }

        try
        {
            IsDetectingBatchSource = true;
            AddLog(LogLevel.Info, $"⚡ Probing batch source auto-discovery for '{query}'...");
            var result = await _autoDiscoveryService.DiscoverAsync(query);
            if (result.Success)
            {
                BatchSourceHost = result.Host;
                BatchSourcePort = result.Port;
                AddLog(LogLevel.Success, $"⚡ Discovered Batch Source: {result.Host}:{result.Port} via {result.DetectionSource}");
            }
            else
            {
                AddLog(LogLevel.Warning, $"Batch source auto-discovery failed: {result.ErrorMessage}");
            }
        }
        finally
        {
            IsDetectingBatchSource = false;
        }
    }

    private async Task AutoDetectBatchDestAsync()
    {
        string query = BatchDestHost;
        if (string.IsNullOrWhiteSpace(query) && BatchAccounts.Count > 0)
        {
            query = BatchAccounts[0].DestUser;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            AddLog(LogLevel.Warning, "Auto-Detect Batch Destination: Please enter a domain or add an account first.");
            return;
        }

        try
        {
            IsDetectingBatchDest = true;
            AddLog(LogLevel.Info, $"⚡ Probing batch destination auto-discovery for '{query}'...");
            var result = await _autoDiscoveryService.DiscoverAsync(query);
            if (result.Success)
            {
                BatchDestHost = result.Host;
                BatchDestPort = result.Port;
                AddLog(LogLevel.Success, $"⚡ Discovered Batch Destination: {result.Host}:{result.Port} via {result.DetectionSource}");
            }
            else
            {
                AddLog(LogLevel.Warning, $"Batch destination auto-discovery failed: {result.ErrorMessage}");
            }
        }
        finally
        {
            IsDetectingBatchDest = false;
        }
    }

    private async Task TestSingleSourceAsync()
    {
        SingleSourceStatus = "Testing...";
        var endpoint = new ServerEndpoint
        {
            Protocol = SingleSourceProtocol,
            Host = SingleSourceHost,
            Port = SingleSourcePort,
            UseSsl = SingleSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var result = await _migrationService.TestConnectionAsync(endpoint, SingleSourceUser, SingleSourcePassword);
        SingleSourceStatus = result.Success ? "✓ Connected OK" : $"✗ Failed: {result.Message}";
        AddLog(result.Success ? LogLevel.Success : LogLevel.Error, $"Source Test: {SingleSourceStatus}");
    }

    private async Task TestSingleDestAsync()
    {
        SingleDestStatus = "Testing...";
        var endpoint = new ServerEndpoint
        {
            Protocol = ServerProtocol.Imap,
            Host = SingleDestHost,
            Port = SingleDestPort,
            UseSsl = SingleDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var result = await _migrationService.TestConnectionAsync(endpoint, SingleDestUser, SingleDestPassword);
        SingleDestStatus = result.Success ? "✓ Connected OK" : $"✗ Failed: {result.Message}";
        AddLog(result.Success ? LogLevel.Success : LogLevel.Error, $"Destination Test: {SingleDestStatus}");
    }

    private bool ConfirmInvalidCertificatesIfEnabled()
    {
        if (!AllowInvalidCertificates) return true;

        var result = DarkMessageBox.Show(
            "Security Warning: 'Permit Self-Signed / Invalid SSL Certificates' is currently enabled in Settings.\n\n" +
            "This bypasses SSL/TLS certificate verification. On untrusted networks, this could allow an attacker to intercept email credentials or message contents (Man-in-the-Middle risk).\n\n" +
            "Do you want to proceed with this migration anyway?",
            "Security Warning: Invalid SSL Allowed",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private async Task StartSingleMigrationAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleSourceHost) || string.IsNullOrWhiteSpace(SingleDestHost))
        {
            DarkMessageBox.Show("Please specify both Source and Destination server hosts.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmInvalidCertificatesIfEnabled())
        {
            return;
        }

        IsSingleMigrating = true;
        _singleCts = new CancellationTokenSource();

        var srcEndpoint = new ServerEndpoint
        {
            Protocol = SingleSourceProtocol,
            Host = SingleSourceHost,
            Port = SingleSourcePort,
            UseSsl = SingleSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var dstEndpoint = new ServerEndpoint
        {
            Protocol = ServerProtocol.Imap,
            Host = SingleDestHost,
            Port = SingleDestPort,
            UseSsl = SingleDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates,
            RootFolder = DstRootFolder
        };

        SingleJob = new AccountJob
        {
            SourceUser = SingleSourceUser,
            SourcePassword = SingleSourcePassword,
            DestUser = SingleDestUser,
            DestPassword = SingleDestPassword
        };

        var options = BuildMigrationOptions();
        var progress = new Progress<AccountJob>(_ => OnPropertyChanged(nameof(SingleJob)));

        try
        {
            await _migrationService.MigrateAccountAsync(srcEndpoint, dstEndpoint, SingleJob, options, progress, _singleCts.Token);
        }
        finally
        {
            IsSingleMigrating = false;
        }
    }

    private void CancelSingleMigration()
    {
        _singleCts?.Cancel();
    }

    private void AddAccount()
    {
        var newAccount = new AccountJob
        {
            SourceUser = "",
            SourcePassword = "",
            DestUser = "",
            DestPassword = "",
            Status = MigrationStatus.Ready,
            StatusMessage = "Ready"
        };
        BatchAccounts.Add(newAccount);
        SelectedAccount = newAccount;

        if (IsBatchRunning)
        {
            AddLog(LogLevel.Info, "Added new account row. Type credentials and click '▶' on the row to queue it into the running batch.");
        }
    }

    private void StartOrQueueAccount(AccountJob account)
    {
        if (string.IsNullOrWhiteSpace(account.SourceUser) || string.IsNullOrWhiteSpace(account.DestUser))
        {
            DarkMessageBox.Show("Please enter valid Source and Destination email addresses for this account.", "Missing Details", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsBatchRunning)
        {
            if (account.Status == MigrationStatus.InProgress)
            {
                AddLog(LogLevel.Warning, $"Account '{account.SourceUser}' is already actively migrating.");
                return;
            }

            bool enqueued = _batchOrchestrator.EnqueueAccount(account);
            if (enqueued)
            {
                AddLog(LogLevel.Success, $"⚡ Enqueued '{account.SourceUser}' into the active running batch.");
            }
            else
            {
                AddLog(LogLevel.Warning, $"Could not enqueue '{account.SourceUser}' (batch may be finishing).");
            }
        }
        else
        {
            account.Status = MigrationStatus.Ready;
            account.StatusMessage = "Ready";
            _ = StartBatchAsync();
        }
    }

    private async Task TestSelectedAccountAsync()
    {
        if (SelectedAccount == null) return;

        if (string.IsNullOrWhiteSpace(BatchSourceHost) || string.IsNullOrWhiteSpace(BatchDestHost))
        {
            DarkMessageBox.Show("Please specify both Source and Destination server hosts in the Batch setup card.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var srcEndpoint = new ServerEndpoint
        {
            Protocol = BatchSourceProtocol,
            Host = BatchSourceHost,
            Port = BatchSourcePort,
            UseSsl = BatchSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var dstEndpoint = new ServerEndpoint
        {
            Protocol = ServerProtocol.Imap,
            Host = BatchDestHost,
            Port = BatchDestPort,
            UseSsl = BatchDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        AddLog(LogLevel.Info, $"Testing credentials for '{SelectedAccount.SourceUser}'...");
        bool success = await _batchOrchestrator.TestSingleAccountAsync(srcEndpoint, dstEndpoint, SelectedAccount);
        if (success)
            AddLog(LogLevel.Success, $"Credentials verified for '{SelectedAccount.SourceUser}'.");
        else
            AddLog(LogLevel.Error, $"Credential test failed for '{SelectedAccount.SourceUser}': {SelectedAccount.StatusMessage}");
    }

    private void CopySourceEmail()
    {
        if (!string.IsNullOrWhiteSpace(SelectedAccount?.SourceUser))
        {
            Clipboard.SetText(SelectedAccount.SourceUser);
            AddLog(LogLevel.Info, $"Copied source email '{SelectedAccount.SourceUser}' to clipboard.");
        }
    }

    private void CopyDestEmail()
    {
        if (!string.IsNullOrWhiteSpace(SelectedAccount?.DestUser))
        {
            Clipboard.SetText(SelectedAccount.DestUser);
            AddLog(LogLevel.Info, $"Copied destination email '{SelectedAccount.DestUser}' to clipboard.");
        }
    }

    private void ClearCompletedAccounts()
    {
        var completedList = BatchAccounts.Where(a => a.Status == MigrationStatus.Completed).ToList();
        if (completedList.Count == 0)
        {
            AddLog(LogLevel.Info, "No completed accounts to clear.");
            return;
        }

        foreach (var acc in completedList)
        {
            BatchAccounts.Remove(acc);
        }
        AddLog(LogLevel.Info, $"Cleared {completedList.Count} completed account(s) from the batch table.");
    }

    private void RemoveAccount()
    {
        if (SelectedAccount != null)
        {
            if (SelectedAccount.Status == MigrationStatus.InProgress)
            {
                DarkMessageBox.Show("Cannot delete an account while it is actively migrating.", "Account Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            BatchAccounts.Remove(SelectedAccount);
        }
    }

    private BatchImportDecision PromptBatchImportMode(int newAccountCount, string sourceDescription)
    {
        if (BatchAccounts.Count == 0)
            return BatchImportDecision.Append;

        return ImportAccountsDialog.Show(Application.Current?.MainWindow, BatchAccounts.Count, newAccountCount, sourceDescription);
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        string text = Clipboard.GetText();
        var accounts = CsvAccountParser.Parse(text);
        if (accounts.Count == 0)
        {
            AddLog(LogLevel.Warning, "No valid account rows found in clipboard text.");
            return;
        }

        if (IsBatchRunning)
        {
            var res = DarkMessageBox.Show(
                $"A batch migration is currently in progress.\n\nDo you want to append these {accounts.Count} account(s) and queue them directly into the running batch?",
                "Queue into Running Batch?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            foreach (var acc in accounts)
            {
                BatchAccounts.Add(acc);
                _batchOrchestrator.EnqueueAccount(acc);
            }
            AddLog(LogLevel.Success, $"Appended and enqueued {accounts.Count} account(s) from clipboard into the active batch.");
            return;
        }

        var decision = PromptBatchImportMode(accounts.Count, "the clipboard");
        if (decision == BatchImportDecision.Cancel)
        {
            AddLog(LogLevel.Info, "Clipboard import cancelled.");
            return;
        }

        if (decision == BatchImportDecision.Replace)
        {
            BatchAccounts.Clear();
        }

        foreach (var acc in accounts) BatchAccounts.Add(acc);

        if (decision == BatchImportDecision.Replace)
            AddLog(LogLevel.Info, $"Replaced batch list with {accounts.Count} accounts pasted from clipboard.");
        else
            AddLog(LogLevel.Info, $"Pasted {accounts.Count} accounts from clipboard (total: {BatchAccounts.Count}).");
    }

    private void LoadCfgFile()
    {
        if (IsBatchRunning) return;
        var dlg = new OpenFileDialog
        {
            Filter = "ImapCopy Config (*.cfg)|*.cfg|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Load ImapCopy.cfg"
        };

        if (dlg.ShowDialog() == true)
        {
            var result = ImapCopyCfgParser.ParseFile(dlg.FileName);
            var fileName = Path.GetFileName(dlg.FileName);

            if (result.Accounts.Count == 0)
            {
                DarkMessageBox.Show("No account directives found in the selected configuration file.", "Empty Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var decision = PromptBatchImportMode(result.Accounts.Count, $"'{fileName}'");
            if (decision == BatchImportDecision.Cancel)
            {
                AddLog(LogLevel.Info, $"Loading '{fileName}' cancelled.");
                return;
            }

            BatchSourceProtocol = result.SourceEndpoint.Protocol;
            BatchSourceHost = result.SourceEndpoint.Host;
            BatchSourcePort = result.SourceEndpoint.Port;
            BatchSourceUseSsl = result.SourceEndpoint.UseSsl;

            BatchDestHost = result.DestEndpoint.Host;
            BatchDestPort = result.DestEndpoint.Port;
            BatchDestUseSsl = result.DestEndpoint.UseSsl;

            if (result.Options.MaxConcurrency > 0)
                Concurrency = result.Options.MaxConcurrency;

            if (decision == BatchImportDecision.Replace)
            {
                BatchAccounts.Clear();
            }

            foreach (var acc in result.Accounts)
                BatchAccounts.Add(acc);

            if (decision == BatchImportDecision.Replace)
                AddLog(LogLevel.Success, $"Replaced batch list with {result.Accounts.Count} accounts from '{fileName}'");
            else
                AddLog(LogLevel.Success, $"Loaded {result.Accounts.Count} accounts from '{fileName}' (total: {BatchAccounts.Count})");
        }
    }

    private void ImportCsvFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV / TSV Files (*.csv;*.tsv)|*.csv;*.tsv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Import Accounts"
        };

        if (dlg.ShowDialog() == true)
        {
            var content = File.ReadAllText(dlg.FileName);
            var accounts = CsvAccountParser.Parse(content);
            var fileName = Path.GetFileName(dlg.FileName);

            if (accounts.Count == 0)
            {
                DarkMessageBox.Show("No valid account rows found in the selected file.", "Empty File", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsBatchRunning)
            {
                var res = DarkMessageBox.Show(
                    $"A batch migration is currently in progress.\n\nDo you want to append these {accounts.Count} account(s) from '{fileName}' and queue them directly into the running batch?",
                    "Queue into Running Batch?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (res != MessageBoxResult.Yes) return;

                foreach (var acc in accounts)
                {
                    BatchAccounts.Add(acc);
                    _batchOrchestrator.EnqueueAccount(acc);
                }
                AddLog(LogLevel.Success, $"Appended and enqueued {accounts.Count} account(s) from '{fileName}' into the active batch.");
                return;
            }

            var decision = PromptBatchImportMode(accounts.Count, $"'{fileName}'");
            if (decision == BatchImportDecision.Cancel)
            {
                AddLog(LogLevel.Info, $"Import from '{fileName}' cancelled.");
                return;
            }

            if (decision == BatchImportDecision.Replace)
            {
                BatchAccounts.Clear();
            }

            foreach (var acc in accounts)
                BatchAccounts.Add(acc);

            if (decision == BatchImportDecision.Replace)
                AddLog(LogLevel.Success, $"Replaced batch list with {accounts.Count} accounts from '{fileName}'");
            else
                AddLog(LogLevel.Success, $"Imported {accounts.Count} accounts from '{fileName}' (total: {BatchAccounts.Count})");
        }
    }

    private void ExportCsvFile()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            Title = "Export Accounts",
            FileName = "migrated_accounts.csv"
        };

        if (dlg.ShowDialog() == true)
        {
            var csv = CsvAccountParser.Export(BatchAccounts);
            File.WriteAllText(dlg.FileName, csv);
            AddLog(LogLevel.Success, $"Exported {BatchAccounts.Count} accounts to '{Path.GetFileName(dlg.FileName)}'");
        }
    }

    private async Task TestAllBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchSourceHost) || string.IsNullOrWhiteSpace(BatchDestHost))
        {
            DarkMessageBox.Show("Please specify both Source and Destination server hosts in the Batch setup card.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBatchRunning = true;
        BatchStatusText = "Testing credentials...";

        var srcEndpoint = new ServerEndpoint
        {
            Protocol = BatchSourceProtocol,
            Host = BatchSourceHost,
            Port = BatchSourcePort,
            UseSsl = BatchSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var dstEndpoint = new ServerEndpoint
        {
            Protocol = ServerProtocol.Imap,
            Host = BatchDestHost,
            Port = BatchDestPort,
            UseSsl = BatchDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        try
        {
            await _batchOrchestrator.TestAllAccountsAsync(srcEndpoint, dstEndpoint, BatchAccounts, Concurrency);
            BatchStatusText = "Credential testing completed.";
        }
        finally
        {
            IsBatchRunning = false;
        }
    }

    private async Task StartBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchSourceHost) || string.IsNullOrWhiteSpace(BatchDestHost))
        {
            DarkMessageBox.Show("Please specify both Source and Destination server hosts in the Batch setup card.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmInvalidCertificatesIfEnabled())
        {
            return;
        }

        IsBatchRunning = true;

        var srcEndpoint = new ServerEndpoint
        {
            Protocol = BatchSourceProtocol,
            Host = BatchSourceHost,
            Port = BatchSourcePort,
            UseSsl = BatchSourceUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates
        };

        var dstEndpoint = new ServerEndpoint
        {
            Protocol = ServerProtocol.Imap,
            Host = BatchDestHost,
            Port = BatchDestPort,
            UseSsl = BatchDestUseSsl,
            AllowInvalidCertificates = AllowInvalidCertificates,
            RootFolder = DstRootFolder
        };

        var options = BuildMigrationOptions();
        options.MaxConcurrency = Concurrency;

        var progress = new Progress<AccountJob>(_ => { });

        try
        {
            await _batchOrchestrator.RunBatchAsync(srcEndpoint, dstEndpoint, BatchAccounts, options, progress);
        }
        finally
        {
            IsBatchRunning = false;
        }
    }

    private void StopBatch()
    {
        _batchOrchestrator.Stop();
    }

    private void ClearBatch()
    {
        BatchAccounts.Clear();
        BatchOverallProgress = 0;
        BatchStatusText = "Ready";
    }

    private void OnBatchProgressUpdated(object? sender, BatchProgressReport report)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            BatchOverallProgress = report.OverallPercentage;
            ActiveWorkersCount = report.ActiveWorkers;
            BatchStatusText = $"{report.CompletedAccounts + report.FailedAccounts} / {report.TotalAccounts} Accounts ({report.OverallPercentage:F0}%) | {report.TotalCopiedMessages} msgs copied | {report.ActiveWorkers} active workers";
        });
    }

    private MigrationOptions BuildMigrationOptions()
    {
        var options = new MigrationOptions
        {
            MaxConcurrency = Concurrency,
            Deduplicate = Deduplicate,
            DstRootFolder = DstRootFolder
        };

        if (!string.IsNullOrWhiteSpace(SkipFolders))
        {
            options.SkipFolders = SkipFolders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        if (!string.IsNullOrWhiteSpace(DenyFlags))
        {
            options.DenyFlags = DenyFlags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        if (EnableDateFilter)
        {
            if (!string.IsNullOrWhiteSpace(SinceDateText) && TryParseDate(SinceDateText, out var since))
            {
                options.SinceDate = since;
            }
            if (!string.IsNullOrWhiteSpace(BeforeDateText) && TryParseDate(BeforeDateText, out var before))
            {
                options.BeforeDate = before;
            }
        }

        return options;
    }

    #endregion

    #region Settings Persistence

    public void SaveCurrentSettings()
    {
        var s = new AppSettings
        {
            SingleSourceHost = SingleSourceHost,
            SingleSourcePort = SingleSourcePort,
            SingleSourceProtocol = SingleSourceProtocol.ToString(),
            SingleSourceUseSsl = SingleSourceUseSsl,
            SingleSourceUser = SingleSourceUser,

            SingleDestHost = SingleDestHost,
            SingleDestPort = SingleDestPort,
            SingleDestUseSsl = SingleDestUseSsl,
            SingleDestUser = SingleDestUser,

            BatchSourceHost = BatchSourceHost,
            BatchSourcePort = BatchSourcePort,
            BatchDestHost = BatchDestHost,
            BatchDestPort = BatchDestPort,
            Concurrency = Concurrency,

            Deduplicate = Deduplicate,
            AllowInvalidCertificates = AllowInvalidCertificates,
            DstRootFolder = DstRootFolder,
            SkipFolders = SkipFolders,
            DenyFlags = DenyFlags,

            EnableDateFilter = EnableDateFilter,
            SelectedDatePreset = SelectedDatePreset,
            SinceDateText = SinceDateText,
            BeforeDateText = BeforeDateText,

            MaskPasswords = MaskPasswords
        };
        SettingsService.Save(s);
    }

    private void LoadSavedSettings()
    {
        var s = SettingsService.Load();

        if (!string.IsNullOrWhiteSpace(s.SingleSourceHost)) _singleSourceHost = s.SingleSourceHost;
        if (s.SingleSourcePort > 0) _singleSourcePort = s.SingleSourcePort;
        if (Enum.TryParse<ServerProtocol>(s.SingleSourceProtocol, true, out var proto))
            _singleSourceProtocol = proto;
        _singleSourceUseSsl = s.SingleSourceUseSsl;
        if (!string.IsNullOrWhiteSpace(s.SingleSourceUser)) _singleSourceUser = s.SingleSourceUser;

        if (!string.IsNullOrWhiteSpace(s.SingleDestHost)) _singleDestHost = s.SingleDestHost;
        if (s.SingleDestPort > 0) _singleDestPort = s.SingleDestPort;
        _singleDestUseSsl = s.SingleDestUseSsl;
        if (!string.IsNullOrWhiteSpace(s.SingleDestUser)) _singleDestUser = s.SingleDestUser;

        if (!string.IsNullOrWhiteSpace(s.BatchSourceHost)) _batchSourceHost = s.BatchSourceHost;
        if (s.BatchSourcePort > 0) _batchSourcePort = s.BatchSourcePort;
        if (!string.IsNullOrWhiteSpace(s.BatchDestHost)) _batchDestHost = s.BatchDestHost;
        if (s.BatchDestPort > 0) _batchDestPort = s.BatchDestPort;
        if (s.Concurrency >= 1 && s.Concurrency <= 16) _concurrency = s.Concurrency;

        _deduplicate = s.Deduplicate;
        _allowInvalidCertificates = s.AllowInvalidCertificates;
        if (!string.IsNullOrWhiteSpace(s.DstRootFolder)) _dstRootFolder = s.DstRootFolder;
        if (!string.IsNullOrWhiteSpace(s.SkipFolders)) _skipFolders = s.SkipFolders;
        if (!string.IsNullOrWhiteSpace(s.DenyFlags)) _denyFlags = s.DenyFlags;

        _enableDateFilter = s.EnableDateFilter;
        if (!string.IsNullOrWhiteSpace(s.SelectedDatePreset)) _selectedDatePreset = s.SelectedDatePreset;
        if (!string.IsNullOrWhiteSpace(s.SinceDateText)) _sinceDateText = s.SinceDateText;
        if (!string.IsNullOrWhiteSpace(s.BeforeDateText)) _beforeDateText = s.BeforeDateText;

        _maskPasswords = s.MaskPasswords;

        if (_enableDateFilter)
        {
            ValidateDateFilter();
        }
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
