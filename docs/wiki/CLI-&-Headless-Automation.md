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
| **Config Path** | `-c, --config <path>` | Path to the `ImapCopy.cfg` configuration file | *(Required for CLI)* |
| **Concurrency** | `-t, --concurrency <n>` | Number of concurrent account worker threads | `4` |
| **Test Only** | `--test` | Verifies logins for all accounts without copying messages | `false` |
| **Help** | `-h, --help` | Displays command line usage and arguments | - |

---

## 🛠️ Usage Examples

### 1. Run Migration using ImapCopy.cfg
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg
```

### 2. High-Speed Multi-Threaded Migration (8 Workers)
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg --concurrency 8
```

### 3. Dry-Run / Test All Credentials Only
```powershell
.\MundofyMailMigrator.exe -c C:\Migrations\ImapCopy.cfg --test
```

### 4. PowerShell Automation & Scheduled Task
```powershell
$logFile = "C:\Migrations\Logs\Migration_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
& "C:\Tools\MundofyMailMigrator.exe" -c "C:\Migrations\ImapCopy.cfg" -t 6 *>&1 | Tee-Object -FilePath $logFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "Mail migration finished successfully!" -ForegroundColor Green
} else {
    Write-Error "Migration encountered errors. Check $logFile"
}
```

---

## 📊 CLI Banner & Output

When run with CLI arguments, the application outputs formatted real-time progress:

```text
=======================================================================
 Mundofy MailMigrator v1.0.0 - CLI Mode
 Created by Danny Schuller | https://mundofy.com
 Issues: https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues
=======================================================================
Parsed 25 accounts from ImapCopy.cfg
[Worker 1] Starting migration: user1@domain.com -> user1@newdomain.com
[Worker 2] Starting migration: user2@domain.com -> user2@newdomain.com
[Worker 1] user1@domain.com: INBOX (1420 messages copied, 0 skipped)
...
-----------------------------------------------------
Batch Migration Summary:
Total Accounts Completed : 25 / 25
Total Messages Copied    : 48,290
Total Errors             : 0
Elapsed Time             : 00:14:32
-----------------------------------------------------
```
