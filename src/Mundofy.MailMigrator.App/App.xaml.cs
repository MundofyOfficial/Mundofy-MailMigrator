using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Mundofy.MailMigrator.Core.Services;

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
        Console.WriteLine(" Mundofy MailMigrator v1.0.0 - CLI Mode");
        Console.WriteLine(" Created by Danny Schuller | https://mundofy.com");
        Console.WriteLine(" Issues: https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues");
        Console.WriteLine("=======================================================================");

        string? configFile = null;
        int concurrency = 4;
        bool testOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if ((arg == "-c" || arg == "--config") && i + 1 < args.Length)
            {
                configFile = args[++i];
            }
            else if ((arg == "-t" || arg == "--threads" || arg == "--concurrency") && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int c)) concurrency = c;
            }
            else if (arg == "--test" || arg == "-t")
            {
                testOnly = true;
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

        Console.WriteLine($"[INFO] Source Server : {parsed.SourceEndpoint.Host}:{parsed.SourceEndpoint.Port} ({(parsed.SourceEndpoint.Protocol == ServerProtocol.Pop3 ? "POP3" : "IMAP")}, SSL: {parsed.SourceEndpoint.UseSsl})");
        Console.WriteLine($"[INFO] Dest Server   : {parsed.DestEndpoint.Host}:{parsed.DestEndpoint.Port} (IMAP, SSL: {parsed.DestEndpoint.UseSsl})");
        Console.WriteLine($"[INFO] Accounts      : {parsed.Accounts.Count}");
        Console.WriteLine($"[INFO] Concurrency   : {parsed.Options.MaxConcurrency} workers");
        Console.WriteLine();

        var service = new ImapMigrationService();
        service.LogEmitted += (s, ev) =>
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ev.Level switch
            {
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };
            Console.WriteLine(ev.ToString());
            Console.ForegroundColor = prevColor;
        };

        var orchestrator = new BatchOrchestrator(service);

        if (testOnly)
        {
            Console.WriteLine("[TEST] Testing credentials for all accounts...");
            await orchestrator.TestAllAccountsAsync(parsed.SourceEndpoint, parsed.DestEndpoint, parsed.Accounts, concurrency);
            int success = parsed.Accounts.Count(a => a.Status == MigrationStatus.Ready);
            Console.WriteLine($"[TEST] Finished testing: {success}/{parsed.Accounts.Count} accounts verified successfully.");
            return;
        }

        Console.WriteLine("[MIGRATE] Starting batch migration...");
        var summary = await orchestrator.RunBatchAsync(parsed.SourceEndpoint, parsed.DestEndpoint, parsed.Accounts, parsed.Options);

        Console.WriteLine();
        Console.WriteLine("----------------- MIGRATION SUMMARY -----------------");
        Console.WriteLine($"Total Accounts Processed : {summary.TotalAccounts}");
        Console.WriteLine($"Successful Accounts      : {summary.SuccessfulAccounts}");
        Console.WriteLine($"Failed Accounts          : {summary.FailedAccounts}");
        Console.WriteLine($"Total Messages Copied    : {summary.TotalMessagesCopied}");
        Console.WriteLine($"Total Errors             : {summary.TotalErrors}");
        Console.WriteLine($"Elapsed Time             : {summary.ElapsedTime:hh\\:mm\\:ss}");
        Console.WriteLine("-----------------------------------------------------");
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Usage: MundofyMailMigrator.exe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  (none)                         Launch the modern Desktop GUI");
        Console.WriteLine("  -c, --config <path>            Path to ImapCopy.cfg file");
        Console.WriteLine("  --concurrency, -t <number>     Number of parallel account migrations (default: 4)");
        Console.WriteLine("  --test                         Test server logins for all accounts without copying");
        Console.WriteLine("  -h, --help                     Show this help information");
        Console.WriteLine();
        Console.WriteLine("Feedback & Bug Reports:");
        Console.WriteLine("  https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues");
        Console.WriteLine();
    }
}
