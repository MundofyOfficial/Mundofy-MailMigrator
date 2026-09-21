# Live Logs, Search & Export Guide

Mundofy MailMigrator provides comprehensive logging, real-time search filtering, structured data exports, and native power management within the **Settings & Logs** tab.

---

## 📝 Live Activity Log

The Live Activity Log displays an event stream for all single and batch operations:

* **Event Structure:** Each log entry records a precise timestamp (`HH:mm:ss`), severity level, and detailed message.
* **Color-Coded Statuses:**
  * 🟢 **Success / Info:** Successful socket connections, folder creations, quota checks, and completed mailbox syncs.
  * 🟡 **Warning:** Skipped duplicate messages, approaching quota limits, or transient connection retries.
  * 🔴 **Error:** Authentication rejections, network timeouts, invalid certificates, or server over-quota errors.
* **Non-Blocking UI:** The logging pipeline uses high-performance event buffering to ensure the user interface remains fluid even when streaming thousands of log lines per minute across 16 concurrent workers.

---

## 🔍 Real-Time Log Search & Filtering

When troubleshooting a migration involving hundreds of thousands of emails, finding specific account errors or folder notifications is instantaneous:

1. Locate the **Search Logs** box on the activity log toolbar.
2. Type any search keyword, account email, folder name, or error code (e.g. `AUTHENTICATIONFAILED`, `INBOX`, `user@domain.com`, or `Error`).
3. The log list filters in real-time as you type.
4. An entry counter displays matching records vs. total records (e.g. `Showing 8 of 1,420 entries`).
5. Click the **❌** button in the search box to clear the filter and display all entries.

---

## 💾 Exporting Activity Logs (.TXT & .CSV)

Administrators frequently need to document migration results for clients or archival audit compliance. MailMigrator supports dual-format log exports:

### 1. Plaintext Export (`.txt`)
* Click **💾 Export Log** and choose **Text Files (*.txt)**.
* Produces a clean, formatted text log suitable for viewing in Notepad, VS Code, or sending to support.
```text
[2026-09-20 14:15:02] [INFO] [Worker 1] Connected to source IMAP server (mail.oldserver.com:993)
[2026-09-20 14:15:03] [INFO] [Worker 1] INBOX: 1,420 messages found. 1,420 copied, 0 skipped.
[2026-09-20 14:15:05] [SUCCESS] [Worker 1] Migration completed successfully for alice@oldschool.com
```

### 2. Structured CSV Export (`.csv`)
* Click **💾 Export Log** and choose **CSV Files (*.csv)**.
* Generates an RFC-compliant comma-separated values file with standard headers:
```csv
Timestamp,Level,Message
2026-09-20 14:15:02,Info,"[Worker 1] Connected to source IMAP server"
2026-09-20 14:15:03,Info,"[Worker 1] INBOX: 1420 messages copied"
2026-09-20 14:15:05,Success,"[Worker 1] Migration completed successfully"
```
* Ideal for importing into Microsoft Excel, Google Sheets, Power BI, or enterprise log analysis tools.

### 💡 Filtered Exporting
If a search filter is active when you click **Export Log**, MailMigrator automatically asks whether you want to export **Only Filtered Entries** or **All Entries**. This makes it easy to generate a dedicated error summary for troubleshooting without exporting millions of normal informational lines.

---

## 💻 Headless CLI Real-Time Log Export

When running automated migrations via the command-line interface or PowerShell scripts, you can export and stream real-time logs to a file using either:

1. **CLI Flag (`-l` / `--log`):**
   ```powershell
   .\MundofyMailMigrator.exe -c ImapCopy.cfg -l C:\Migrations\Logs\migration.txt
   ```
2. **`ImapCopy.cfg` Directive (`LogFile`):**
   ```ini
   LogFile "C:\Migrations\Logs\migration.txt"
   ```

All progress events, worker connections, copied messages, skipped duplicates, errors, and the final migration summary are written to the file in real-time with automatic flushing.

---

## ⚡ Native PC Sleep Prevention

Large mailbox migrations can take hours or run overnight. If the operating system enters idle sleep or standby, active TCP sockets disconnect, causing migrations to fail.

* **How It Works:** MailMigrator integrates with Windows native power management APIs (`SetThreadExecutionState` with `ES_CONTINUOUS | ES_SYSTEM_REQUIRED`).
* **Display Energy Saving:** Your computer will stay awake to continue processing network sockets, while allowing your monitors/screens to turn off and save power.
* **Configuration:** In **Settings & Logs**, the setting **"Prevent Computer Sleep During Migration"** is enabled by default. You can toggle this setting at any time.
* **Persistence:** Application preferences and settings are stored locally in `%APPDATA%\Mundofy\MailMigrator\settings.json`.
