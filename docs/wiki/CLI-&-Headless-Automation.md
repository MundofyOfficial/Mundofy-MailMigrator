# CLI & Headless Automation Guide

Mundofy MailMigrator provides a high-performance command-line interface (CLI) for sysadmins, DevOps engineers, and automated server workflows.

---

## 💻 Syntax & Commands

```powershell
MundofyMailMigrator.exe [options]
```

### Options Reference

| Option | Flag | Description | Default |
| :--- | :--- | :--- | :--- |
| **Config Path** | `-c, --config <path>` | Path to the `ImapCopy.cfg` configuration file | *(Auto-detects `ImapCopy.cfg` in current directory)* |
| **Log File Export** | `-l, --log <path>` | Real-time export/stream of all logs to a specified text file | *(Console only)* |
| **Concurrency** | `-t, --threads, --concurrency <n>` | Number of concurrent account worker threads | `4` |
| **Allow Invalid SSL** | `-k, --allow-invalid-certs, --insecure` | Permit self-signed, untrusted, or unsigned SSL certificates | `false` |
| **Test Only** | `--test` | Verifies logins for all accounts without copying messages | `false` |
| **Help** | `-h, --help` | Displays command line usage and arguments | - |

---

## 🛠️ Usage Examples

### 1. Run Migration using ImapCopy.cfg
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg
```

### 2. Export Logs to File (`-l`)
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg -l C:\Migrations\Logs\migration.txt
```

### 3. Permit Self-Signed / Unsigned SSL Certificates (`-k`)
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg -k
```

### 4. High-Speed Multi-Threaded Migration (8 Workers)
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg --concurrency 8 -l migration.log
```

### 5. Dry-Run / Test All Credentials Only
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg --test
```

### 6. PowerShell Automation & Scheduled Task
```powershell
& "C:\Tools\MundofyMailMigrator.exe" -c "C:\Migrations\ImapCopy.cfg" -t 6 -k -l "C:\Migrations\Logs\latest.txt"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Mail migration finished successfully!" -ForegroundColor Green
} else {
    Write-Error "Migration encountered errors. Check log file."
}
```

---

## 📊 Formatted CLI Output

When run with CLI arguments, the application outputs formatted, color-coded console logs:

```text
=======================================================================
 Mundofy MailMigrator - CLI Mode
 Created by Danny Schuller | https://mundofy.com
 Issues: https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues
=======================================================================
[INFO] Loading configuration: ImapCopy.cfg
[INFO] Source Server : mail.oldserver.com:993 (IMAP, SSL: True)
[INFO] Dest Server   : mail.newserver.com:993 (IMAP, SSL: True)
[INFO] Accounts      : 25
[INFO] Concurrency   : 4 workers

[Worker 1] Starting migration: user1@domain.com -> user1@newdomain.com
[Worker 2] Starting migration: user2@domain.com -> user2@newdomain.com
[Worker 1] user1@domain.com: INBOX (1,420 messages copied, 0 skipped)
...
----------------- MIGRATION SUMMARY -----------------
Total Accounts Processed : 25
Successful Accounts      : 25
Failed Accounts          : 0
Total Messages Copied    : 48,290
Total Errors             : 0
Elapsed Time             : 00:14:32
-----------------------------------------------------
```

---

## 🐧 Cross-Platform Linux & Docker CLI (Roadmap)

While the desktop GUI currently targets Windows (WPF) and macOS (Avalonia UI 12), a lightweight, native cross-platform console build (`linux-x64`, `linux-arm64`) and an official Docker container (`docker run mundofy/mailmigrator ...`) are under active development. See the [Planned Features & Roadmap](Roadmap) for details.
