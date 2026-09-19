using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mundofy.MailMigrator.Core.Models;

public enum MigrationStatus
{
    Pending,
    Testing,
    Ready,
    Queued,
    InProgress,
    Completed,
    Failed,
    Paused,
    Skipped
}

public class AccountJob : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _sourceUser = string.Empty;
    public string SourceUser
    {
        get => _sourceUser;
        set => SetField(ref _sourceUser, value);
    }

    private string _sourcePassword = string.Empty;
    public string SourcePassword
    {
        get => _sourcePassword;
        set => SetField(ref _sourcePassword, value);
    }

    private string _destUser = string.Empty;
    public string DestUser
    {
        get => _destUser;
        set => SetField(ref _destUser, value);
    }

    private string _destPassword = string.Empty;
    public string DestPassword
    {
        get => _destPassword;
        set => SetField(ref _destPassword, value);
    }

    private MigrationStatus _status = MigrationStatus.Pending;
    public MigrationStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    private string _statusMessage = "Pending";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private int _totalMessages = 0;
    public int TotalMessages
    {
        get => _totalMessages;
        set
        {
            if (SetField(ref _totalMessages, value))
                OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    private int _copiedMessages = 0;
    public int CopiedMessages
    {
        get => _copiedMessages;
        set
        {
            if (SetField(ref _copiedMessages, value))
                OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    private int _skippedMessages = 0;
    public int SkippedMessages
    {
        get => _skippedMessages;
        set
        {
            if (SetField(ref _skippedMessages, value))
                OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    private int _failedMessages = 0;
    public int FailedMessages
    {
        get => _failedMessages;
        set
        {
            if (SetField(ref _failedMessages, value))
                OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    private long _bytesTransferred = 0;
    public long BytesTransferred
    {
        get => _bytesTransferred;
        set => SetField(ref _bytesTransferred, value);
    }

    private string _currentFolder = string.Empty;
    public string CurrentFolder
    {
        get => _currentFolder;
        set => SetField(ref _currentFolder, value);
    }

    private string _transferSpeed = string.Empty;
    public string TransferSpeed
    {
        get => _transferSpeed;
        set => SetField(ref _transferSpeed, value);
    }

    private MailboxQuotaInfo? _sourceQuota;
    public MailboxQuotaInfo? SourceQuota
    {
        get => _sourceQuota;
        set => SetField(ref _sourceQuota, value);
    }

    private MailboxQuotaInfo? _destQuota;
    public MailboxQuotaInfo? DestQuota
    {
        get => _destQuota;
        set => SetField(ref _destQuota, value);
    }

    public double ProgressPercentage
    {
        get
        {
            if (TotalMessages <= 0)
                return Status == MigrationStatus.Completed ? 100.0 : 0.0;
            return Math.Clamp((CopiedMessages + SkippedMessages + FailedMessages) * 100.0 / TotalMessages, 0.0, 100.0);
        }
    }

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
}
