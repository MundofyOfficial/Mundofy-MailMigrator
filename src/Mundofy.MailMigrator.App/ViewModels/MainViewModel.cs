using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Mundofy.MailMigrator.Core.Services;

namespace Mundofy.MailMigrator.App.ViewModels;

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
        RemoveAccountCommand = new RelayCommand(RemoveAccount, () => SelectedAccount != null && !IsBatchRunning);
        DeleteAccountRowCommand = new RelayCommand(param =>
        {
            if (param is AccountJob job)
            {
                BatchAccounts.Remove(job);
            }
            else if (SelectedAccount != null)
            {
                BatchAccounts.Remove(SelectedAccount);
            }
        }, _ => !IsBatchRunning);
        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard);
        LoadCfgCommand = new RelayCommand(LoadCfgFile);
        ImportCsvCommand = new RelayCommand(ImportCsvFile);
        ExportCsvCommand = new RelayCommand(ExportCsvFile, () => BatchAccounts.Count > 0);
        TestAllBatchCommand = new RelayCommand(async () => await TestAllBatchAsync(), () => !IsBatchRunning && BatchAccounts.Count > 0);
        StartBatchCommand = new RelayCommand(async () => await StartBatchAsync(), () => !IsBatchRunning && BatchAccounts.Count > 0);
        StopBatchCommand = new RelayCommand(StopBatch, () => IsBatchRunning);
        ClearBatchCommand = new RelayCommand(ClearBatch, () => !IsBatchRunning);
        ClearLogsCommand = new RelayCommand(ClearLogs);

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

        AddLog(LogLevel.Info, "Mundofy MailMigrator v1.1.0 initialized.");
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
    public RelayCommand PasteFromClipboardCommand { get; }
    public RelayCommand LoadCfgCommand { get; }
    public RelayCommand ImportCsvCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand TestAllBatchCommand { get; }
    public RelayCommand StartBatchCommand { get; }
    public RelayCommand StopBatchCommand { get; }
    public RelayCommand ClearBatchCommand { get; }
    public RelayCommand ClearLogsCommand { get; }

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

    private async Task StartSingleMigrationAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleSourceHost) || string.IsNullOrWhiteSpace(SingleDestHost))
        {
            MessageBox.Show("Please specify both Source and Destination server hosts.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        BatchAccounts.Add(new AccountJob
        {
            SourceUser = $"user{BatchAccounts.Count + 1}@domain.com",
            SourcePassword = "",
            DestUser = $"user{BatchAccounts.Count + 1}@dest.com",
            DestPassword = "",
            Status = MigrationStatus.Ready,
            StatusMessage = "Ready"
        });
    }

    private void RemoveAccount()
    {
        if (SelectedAccount != null)
            BatchAccounts.Remove(SelectedAccount);
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        string text = Clipboard.GetText();
        var accounts = CsvAccountParser.Parse(text);
        if (accounts.Count > 0)
        {
            foreach (var acc in accounts) BatchAccounts.Add(acc);
            AddLog(LogLevel.Info, $"Pasted {accounts.Count} accounts from clipboard.");
        }
    }

    private void LoadCfgFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "ImapCopy Config (*.cfg)|*.cfg|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Load ImapCopy.cfg"
        };

        if (dlg.ShowDialog() == true)
        {
            var result = ImapCopyCfgParser.ParseFile(dlg.FileName);
            BatchSourceProtocol = result.SourceEndpoint.Protocol;
            BatchSourceHost = result.SourceEndpoint.Host;
            BatchSourcePort = result.SourceEndpoint.Port;
            BatchSourceUseSsl = result.SourceEndpoint.UseSsl;

            BatchDestHost = result.DestEndpoint.Host;
            BatchDestPort = result.DestEndpoint.Port;
            BatchDestUseSsl = result.DestEndpoint.UseSsl;

            if (result.Options.MaxConcurrency > 0)
                Concurrency = result.Options.MaxConcurrency;

            BatchAccounts.Clear();
            foreach (var acc in result.Accounts)
                BatchAccounts.Add(acc);

            AddLog(LogLevel.Success, $"Loaded {result.Accounts.Count} accounts from '{Path.GetFileName(dlg.FileName)}'");
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
            foreach (var acc in accounts) BatchAccounts.Add(acc);
            AddLog(LogLevel.Success, $"Imported {accounts.Count} accounts from '{Path.GetFileName(dlg.FileName)}'");
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
        IsBatchRunning = true;
        BatchStatusText = "Testing credentials in parallel...";

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
            MessageBox.Show("Please specify both Source and Destination server hosts in the Batch setup card.", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        return options;
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
