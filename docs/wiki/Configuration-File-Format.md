# Configuration File Format (`ImapCopy.cfg`)

Mundofy MailMigrator maintains 100% backward compatibility with the legacy `ImapCopy.cfg` format used by Armin Diehl's classic tool, while enhancing it with native TLS 1.3 encryption and intelligent Message-ID deduplication.

---

## 📄 File Syntax

The configuration file is case-insensitive. Lines beginning with `#` or `//` are treated as comments and ignored.

```ini
# ==========================================================
# Mundofy MailMigrator Configuration File
# ==========================================================

# Source Server Configuration
SourceServer  mail.oldschool.com
SourcePort    993

# Destination Server Configuration
DestServer    mail.newschool.com
DestPort      993

# Migration Directives
CreateDstFolder     Yes
SkipEmptyFolders    No

# Account Definitions: "Copy" followed by source and destination pairs
# Format: Copy "source_user" "source_password" "dest_user" "dest_password"
Copy "alice@oldschool.com"   "SecretPass123!"   "alice@newschool.com"   "NewSecretPass1!"
Copy "bob@oldschool.com"     "B0bSecurePass#"   "bob@newschool.com"     "B0bNewPass2026!"
Copy "sales@oldschool.com"   "SalesDept2026"    "sales@newschool.com"   "SalesDept2026"
```

---

## ⚙️ Directives Reference

| Directive | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `SourceServer` | Hostname / IP | *Required* | Hostname or IP address of the source IMAP/POP3 server. |
| `SourcePort` | Integer | `993` | Port of source server (`993` SSL, `143` plain/STARTTLS). |
| `DestServer` | Hostname / IP | *Required* | Hostname or IP address of the destination IMAP server. |
| `DestPort` | Integer | `993` | Port of destination server (`993` SSL, `143` plain/STARTTLS). |
| `CreateDstFolder` | `Yes` / `No` | `Yes` | Automatically creates missing subfolders on destination. |
| `SkipEmptyFolders` | `Yes` / `No` | `No` | Skips folders with 0 messages. |
| `Copy` | Account Mapping | - | Maps source user/pass to destination user/pass. Quotes are recommended. |

---

## 📊 CSV & TSV Format (Alternative)

In addition to `ImapCopy.cfg`, the Desktop GUI supports importing `.csv` and `.tsv` files:

```csv
SourceUser,SourcePassword,DestUser,DestPassword
alice@domain.com,Secret123,alice@newdomain.com,NewSecret456
bob@domain.com,Pass789,bob@newdomain.com,NewPass012
```
