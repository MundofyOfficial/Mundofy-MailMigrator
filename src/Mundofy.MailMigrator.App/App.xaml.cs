using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Mundofy.MailMigrator.Core;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Mundofy.MailMigrator.Core.Services;
using Mundofy.MailMigrator.App.Services;

namespace Mundofy.MailMigrator.App;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0)
        {
            if (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                var stdOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdOut);
                Console.SetError(stdOut);
            }
            await RunCliAsync(e.Args);
            Shutdown(0);
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static async Task RunCliAsync(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("=======================================================================");
        Console.WriteLine($" Mundofy MailMigrator v{AppVersion.Current} - CLI Mode");
        Console.WriteLine(" Created by Danny Schuller | https://mundofy.com");
        Console.WriteLine(" Issues: https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues");
        Console.WriteLine("=======================================================================");

        string? configFile = null;
        string? logFilePath = null;
        int concurrency = 4;
        bool testOnly = false;
        bool allowInvalidCerts = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if ((arg == "-c" || arg == "--config") && i + 1 < args.Length)
            {
                configFile = args[++i];
            }
            else if ((arg == "-l" || arg == "--log" || arg == "--log-file" || arg == "--export-log") && i + 1 < args.Length)
            {
                logFilePath = args[++i];
            }
            else if ((arg == "-t" || arg == "--threads" || arg == "--concurrency") && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int c)) concurrency = c;
            }
            else if (arg == "--test" || arg == "-t")
            {
                testOnly = true;
            }
            else if (arg == "-k" || arg == "--insecure" || arg == "--allow-invalid-certs" || arg == "--allow-invalid-certificates" || arg == "--permit-unsigned-ssl")
            {
                allowInvalidCerts = true;
            }
            else if (arg == "-h" || arg == "--help")
            {
                PrintHelp();
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(configFile))
        {
            if (File.Exists("ImapCopy.cfg"))
                configFile = "ImapCopy.cfg";
            else if (File.Exists("imapcopy.cfg"))
                configFile = "imapcopy.cfg";
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No configuration file specified and ImapCopy.cfg not found in current directory.");
                Console.ResetColor();
                PrintHelp();
                return;
            }
        }

        Console.WriteLine($"[INFO] Loading configuration: {configFile}");
        var parsed = ImapCopyCfgParser.ParseFile(configFile);
        if (concurrency > 0) parsed.Options.MaxConcurrency = concurrency;
        if (allowInvalidCerts)
        {
            parsed.SourceEndpoint.AllowInvalidCertificates = true;
            parsed.DestEndpoint.AllowInvalidCertificates = true;
        }

        if (string.IsNullOrWhiteSpace(logFilePath) && !string.IsNullOrWhiteSpace(parsed.LogFilePath))
        {
            logFilePath = parsed.LogFilePath;
        }

        StreamWriter? fileLogger = null;
        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                fileLogger = new StreamWriter(new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
                Console.WriteLine($"[INFO] Logging output to: {Path.GetFullPath(logFilePath)}");
                fileLogger.WriteLine("=======================================================================");
                fileLogger.WriteLine($" Mundofy MailMigrator v{AppVersion.Current} - CLI Log");
                fileLogger.WriteLine($" Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                fileLogger.WriteLine("=======================================================================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] Failed to initialize log file '{logFilePath}': {ex.Message}");
                Console.ResetColor();
            }
        }

        string srcInsecure = parsed.SourceEndpoint.AllowInvalidCertificates ? ", Allow Invalid SSL: True" : "";
        string dstInsecure = parsed.DestEndpoint.AllowInvalidCertificates ? ", Allow Invalid SSL: True" : "";

        Console.WriteLine($"[INFO] Source Server : {parsed.SourceEndpoint.Host}:{parsed.SourceEndpoint.Port} ({(parsed.SourceEndpoint.Protocol == ServerProtocol.Pop3 ? "POP3" : "IMAP")}, SSL: {parsed.SourceEndpoint.UseSsl}{srcInsecure})");
        Console.WriteLine($"[INFO] Dest Server   : {parsed.DestEndpoint.Host}:{parsed.DestEndpoint.Port} (IMAP, SSL: {parsed.DestEndpoint.UseSsl}{dstInsecure})");
        Console.WriteLine($"[INFO] Accounts      : {parsed.Accounts.Count}");
        Console.WriteLine($"[INFO] Concurrency   : {parsed.Options.MaxConcurrency} workers");
        Console.WriteLine();

        Action<string> logBoth = line =>
        {
            if (fileLogger != null)
            {
                lock (fileLogger)
                {
                    fileLogger.WriteLine(line);
                }
            }
        };

        var service = new ImapMigrationService();
        service.LogEmitted += (s, ev) =>
        {
            var line = ev.ToString();
            logBoth(line);

            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ev.Level switch
            {
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };
            Console.WriteLine(line);
            Console.ForegroundColor = prevColor;
        };

        var orchestrator = new BatchOrchestrator(service);

        try
        {
            if (testOnly)
            {
                Console.WriteLine("[TEST] Testing credentials for all accounts...");
                logBoth("[TEST] Testing credentials for all accounts...");
                await orchestrator.TestAllAccountsAsync(parsed.SourceEndpoint, parsed.DestEndpoint, parsed.Accounts, concurrency);
                int success = parsed.Accounts.Count(a => a.Status == MigrationStatus.Ready);
                string testSummary = $"[TEST] Finished testing: {success}/{parsed.Accounts.Count} accounts verified successfully.";
                Console.WriteLine(testSummary);
                logBoth(testSummary);
                return;
            }

            Console.WriteLine("[MIGRATE] Starting batch migration...");
            logBoth("[MIGRATE] Starting batch migration...");
            MigrationSummary summary;
            SleepPreventionService.Acquire();
            try
            {
                summary = await orchestrator.RunBatchAsync(parsed.SourceEndpoint, parsed.DestEndpoint, parsed.Accounts, parsed.Options);
            }
            finally
            {
                SleepPreventionService.Release();
            }

            Console.WriteLine();
            logBoth("");
            string[] summaryLines =
            [
                "----------------- MIGRATION SUMMARY -----------------",
                $"Total Accounts Processed : {summary.TotalAccounts}",
                $"Successful Accounts      : {summary.SuccessfulAccounts}",
                $"Failed Accounts          : {summary.FailedAccounts}",
                $"Total Messages Copied    : {summary.TotalMessagesCopied}",
                $"Total Errors             : {summary.TotalErrors}",
                $"Elapsed Time             : {summary.ElapsedTime:hh\\:mm\\:ss}",
                "-----------------------------------------------------"
            ];

            foreach (var sLine in summaryLines)
            {
                Console.WriteLine(sLine);
                logBoth(sLine);
            }
        }
        finally
        {
            if (fileLogger != null)
            {
                fileLogger.Flush();
                fileLogger.Dispose();
                Console.WriteLine($"[INFO] Log successfully saved to {Path.GetFullPath(logFilePath!)}");
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Usage: MundofyMailMigrator.exe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  (none)                         Launch the modern Desktop GUI");
        Console.WriteLine("  -c, --config <path>            Path to ImapCopy.cfg file");
        Console.WriteLine("  -l, --log <path>               Export real-time migration logs to a file (e.g. migration.txt)");
        Console.WriteLine("  --concurrency, -t <number>     Number of parallel account migrations (default: 4)");
        Console.WriteLine("  -k, --allow-invalid-certs      Permit self-signed, untrusted, or invalid SSL certificates");
        Console.WriteLine("      --insecure                 Alias for --allow-invalid-certs");
        Console.WriteLine("  --test                         Test server logins for all accounts without copying");
        Console.WriteLine("  -h, --help                     Show this help information");
        Console.WriteLine();
        Console.WriteLine("Feedback & Bug Reports:");
        Console.WriteLine("  https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues");
        Console.WriteLine();
    }
}
