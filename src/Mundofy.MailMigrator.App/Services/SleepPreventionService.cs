using System.Runtime.InteropServices;

namespace Mundofy.MailMigrator.App.Services;

/// <summary>
/// Controls Windows power management to prevent idle sleep / standby during active migrations.
/// Uses SetThreadExecutionState (ES_CONTINUOUS | ES_SYSTEM_REQUIRED).
/// </summary>
public static class SleepPreventionService
{
    [Flags]
    private enum ExecutionState : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_CONTINUOUS = 0x80000000
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    private static int _activeCount = 0;
    private static readonly object _lock = new();

    /// <summary>
    /// Indicates whether system sleep prevention is actively engaged.
    /// </summary>
    public static bool IsSleepPrevented
    {
        get
        {
            lock (_lock)
            {
                return _activeCount > 0;
            }
        }
    }

    /// <summary>
    /// Prevents the computer from entering sleep mode while retaining monitor power-saving.
    /// </summary>
    public static void Acquire()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        lock (_lock)
        {
            _activeCount++;
            if (_activeCount == 1)
            {
                try
                {
                    SetThreadExecutionState(ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED);
                }
                catch
                {
                    // Ignore gracefully if OS denies or on non-desktop platforms
                }
            }
        }
    }

    /// <summary>
    /// Releases a sleep prevention lock. When all locks are released, standard Windows sleep behavior resumes.
    /// </summary>
    public static void Release()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        lock (_lock)
        {
            if (_activeCount > 0)
            {
                _activeCount--;
                if (_activeCount == 0)
                {
                    try
                    {
                        SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
                    }
                    catch
                    {
                        // Ignore gracefully
                    }
                }
            }
        }
    }

    /// <summary>
    /// Forces complete release of all locks and restores normal Windows sleep.
    /// </summary>
    public static void Reset()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        lock (_lock)
        {
            _activeCount = 0;
            try
            {
                SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
            }
            catch
            {
                // Ignore gracefully
            }
        }
    }
}
