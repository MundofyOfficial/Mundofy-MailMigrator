# Configuration File Format (`ImapCopy.cfg` & CSV)

Mundofy MailMigrator maintains 100% backward compatibility with the legacy `ImapCopy.cfg` configuration file format used by Armin Diehl's classic tool, while adding support for modern multi-format CSV spreadsheets and clipboard imports.

---

## 📄 Legacy `ImapCopy.cfg` Syntax

The configuration file is case-insensitive. Lines beginning with `#` or `//` are treated as comments and ignored.

```ini
# ==========================================================
# Mundofy MailMigrator Configuration File (ImapCopy.cfg)
# ==========================================================

# Source Server Configuration
SourceProtocol  IMAP
SourceServer    mail.oldschool.com
SourcePort      993

# Destination Server Configuration
DestServer      mail.newschool.com
DestPort        993

# Concurrency & Performance
Concurrency     4

# Security Directives
AllowInvalidCertificates  No

# Real-Time Logging
LogFile         "migration.txt"

# Folder Filtering Directives
#skipfolder     "INBOX.Trash"
#skipfolder     "INBOX.Spam"
#skipmatch      "INBOX.Temp."
#copyfolder     "INBOX"
#DstRootFolder  "OldArchive"

# IMAP Flags
DenyFlags       "\Recent"

# Account Definitions: "Copy" followed by source and destination pairs
# Format: Copy "source_user" "source_password" "dest_user" "dest_password"
# Use * as wildcard if destination matches source
Copy "alice@oldschool.com"   "SecretPass123!"   "alice@newschool.com"   "NewSecretPass1!"
Copy "bob@oldschool.com"     "B0bSecurePass#"   "bob@newschool.com"     "B0bNewPass2026!"
Copy "sales@oldschool.com"   "SalesDept2026"    "sales@newschool.com"   "SalesDept2026"
```

---

## ⚙️ Directives Reference

| Directive | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `SourceProtocol` | `IMAP` / `POP3` | `IMAP` | Protocol to use for the source server. |
| `SourceServer` | Hostname / IP | *Required* | Hostname or IP address of the source IMAP/POP3 server. |
| `SourcePort` | Integer | `993` | Port of source server (`993` SSL, `995` POP3 SSL, `143`/`110` plain/STARTTLS). |
| `DestServer` | Hostname / IP | *Required* | Hostname or IP address of the destination IMAP server. |
| `DestPort` | Integer | `993` | Port of destination server (`993` SSL, `143` plain/STARTTLS). |
| `Concurrency` | Integer (1–32) | `4` | Number of concurrent account worker threads. (Aliases: `Threads`, `Workers`). |
| `AllowInvalidCertificates` | `Yes` / `No` | `No` | Permit self-signed, untrusted, or unsigned SSL certificates. (Aliases: `AllowInvalidCerts`, `PermitUnsignedSSL`). |
| `LogFile` | File Path | - | Path to write real-time migration logs to (e.g. `migration.txt`). (Aliases: `Log`, `ExportLog`). |
| `skipfolder` | Folder Name | - | Explicit folder name to skip (e.g. `skipfolder "INBOX.Trash"`). |
| `skipmatch` | Prefix String | - | Prefix of folders to skip (e.g. `skipmatch "INBOX.Temp."`). |
| `copyfolder` | Folder Name | - | If specified, ONLY matching folders are migrated. |
| `DstRootFolder` | Folder Name | - | Creates the source folder hierarchy under a destination subfolder instead of root. |
| `DenyFlags` | Space-delimited | `\Recent` | Specific IMAP flags to strip before copying to destination. |
| `AllowFlags` | Space-delimited | - | Explicit whitelist of IMAP flags to preserve. |
| `converttimezone` | `SRC` `DST` | - | Timezone offset mapping (e.g. `converttimezone "UTC" "+0000"`). |
| `CreateEmptyFolders` | `Yes` / `No` | `No` | Recreates folders on destination even if they contain 0 messages. |
| `Copy` | Account Mapping | - | Maps source user/pass to destination user/pass. Quotes are recommended. |

---

## 📊 CSV & TSV Formats (Smart Parser v1.5.2)

In addition to `ImapCopy.cfg`, Mundofy MailMigrator includes an intelligent multi-format CSV/TSV parser that auto-detects column headers and accommodates various enterprise migration scenarios:

### 1. Classic 4-Column Format (Username + Password)
Standard spreadsheet format used when migrating standard IMAP/POP3 accounts with passwords:
```csv
SourceUser,SourcePassword,DestUser,DestPassword
alice@oldschool.com,Secret123,alice@newschool.com,NewSecret456
bob@oldschool.com,Pass789,bob@newschool.com,NewPass012
```

### 2. 2-Column Email Mapping Format (Passwordless OAuth2)
When migrating between Microsoft 365 or Google Workspace tenants where admin OAuth2 credentials are configured, no passwords are required. The smart parser automatically recognizes 2-column email mappings without mistaking destination emails for source passwords:
```csv
SourceEmail,DestEmail
alice@oldschool.com,alice@newschool.com
bob@oldschool.com,bob@newschool.com
carol@oldschool.com,carol@newschool.com
```

### 3. 1-Column Single Mailbox List (Admin Center Export)
When performing intra-tenant reorganization or when destination email addresses match source addresses exactly:
```csv
Mailbox
alice@company.com
bob@company.com
carol@company.com
```

### 💡 Auto-Detected Column Headers
The parser recognizes standard case-insensitive headers:
* **Source Mailbox:** `SourceUser`, `SourceEmail`, `Source`, `User`, `Email`, `Mailbox`
* **Source Password:** `SourcePassword`, `SourcePass`, `Password`, `Pass`
* **Destination Mailbox:** `DestUser`, `DestEmail`, `Dest`, `TargetUser`, `TargetEmail`
* **Destination Password:** `DestPassword`, `DestPass`, `TargetPassword`, `TargetPass`
