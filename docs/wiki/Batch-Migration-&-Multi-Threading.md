# Batch Migration & Concurrency Guide

The **Batch Migration** tab enables migrating dozens, hundreds, or thousands of mailboxes concurrently in a single high-speed session with adaptive multi-threading, intelligent CSV parsing, and enterprise cloud authentication.

---

## ⚡ Concurrency & Worker Scaling

Mundofy MailMigrator includes an asynchronous multi-threaded orchestrator:
* **Worker Slider:** Adjust parallel workers from **1 to 16 threads** (default: 4).
* **Throughput Optimization:**
  * **1–2 Workers:** Ideal for metered connections or servers with strict IP rate limits.
  * **4–8 Workers:** Recommended for standard broadband and fiber connections migrating corporate domains.
  * **8–16 Workers:** Recommended for high-bandwidth dedicated servers or cloud VMs handling large domain migrations.

---

## 🏢 Protocol & Enterprise Cloud Authentication

MailMigrator supports both standard server credentials and enterprise tenant-wide modern authentication:

### 1. Standard IMAP / POP3 Servers
* Set the default Source and Destination server hosts and ports.
* Passwords for each mailbox are supplied via the accounts table or CSV import.

### 2. Microsoft 365 Enterprise Batch (Zero User Passwords)
* Select **Microsoft 365** as the protocol.
* Enter your **Tenant ID (Directory ID)**, **Application (Client) ID**, and **Client Secret** (see [Modern OAuth2 Guide](Modern-OAuth2-&-Cloud-Providers) for Azure App Registration steps).
* MailMigrator automatically handles per-mailbox token generation dynamically. User passwords are not required!

### 3. Google Workspace Enterprise Batch (Service Account JSON)
* Select **Google Workspace** as the protocol.
* Click **Browse** and load your Google Cloud **Service Account JSON key** configured with Domain-Wide Delegation.
* MailMigrator signs JWT assertions and impersonates each target mailbox subject automatically without passwords.

### 4. Dynamic Column Adaptation (v1.5.2)
When Microsoft 365 or Google Workspace is selected:
* Username column headers automatically update to **Source Mailbox (Email)** or **Dest Mailbox (Email)**.
* Password column headers update to **Source Auth** or **Dest Auth** displaying a clean `🔑 Admin OAuth2` badge.
* Password cell text editing is automatically disabled to eliminate operator confusion.

---

## 📋 Adding & Managing Batch Accounts

The batch account table supports four flexible input methods:

### 1. Smart CSV Import (v1.5.2)
Click **📄 Import CSV** to import any spreadsheet. The parser automatically detects headers and accommodates multiple enterprise formats:
* **2-Column Email Mapping:** `source@domain.com, dest@newdomain.com` (ideal for enterprise tenant-to-tenant migrations without passwords).
* **1-Column Mailbox List:** `user@domain.com` (ideal for exporting raw user lists directly from Google Admin or Microsoft 365 Admin centers).
* **4-Column Classic:** `SourceUser, SourcePassword, DestUser, DestPassword` (for standard password-based IMAP/POP3 migrations).

### 2. Clipboard Paste (Excel & Tab-Separated)
Copy columns directly from Microsoft Excel, Google Sheets, or Notepad and click **📋 Paste Clipboard**.

### 3. Loading Legacy `ImapCopy.cfg`
Click **📂 Load ImapCopy.cfg** to load config files (a complete starter template is provided at `Dist/ImapCopy.cfg`). Server settings, concurrency, security flags, and all account pairs are automatically loaded into the table.

### 4. Direct Table Management
* **➕ Add Account:** Inserts a new blank row into the DataGrid.
* **➖ Remove Selected:** Deletes the currently highlighted row.
* **🗑 Quick Row Delete:** 1-click red trash button on each row.
* **Keyboard Delete:** Highlight any row and press the `Delete` key.
* **Inline Editing:** Double-click any cell to modify email addresses or passwords.

---

## 🔍 Pre-Flight Verification & Quotas

Before executing a large migration:
1. **🔍 Test All Credentials:** Verifies connections and logins in parallel. Rows turn green (**Ready**) or red (**Auth Failed**).
2. **📊 Check Quotas:** Interrogates source and destination storage limits in parallel (RFC 2087 IMAP QUOTA & POP3 sizing). Rows display health badges:
   * 🟢 **Normal** (`< 80%`)
   * 🟡 **Warning** (`80% – 94%`)
   * 🟠 **Critical** (`95% – 99%`)
   * 🔴 **Exceeded** (`≥ 100%`)
   * 🔵 **Unlimited** (Unmetered storage)
3. **Pre-Flight Capacity Guard:** If any destination mailbox lacks enough free capacity to receive the source mailbox contents, an alert warning is presented before transfer begins.

---

## ▶ Executing the Batch & Interactive Controls

1. Click **▶ Start Batch Migration**.
2. **Per-Account Pause & Resume:**
   * Need to inspect or adjust a specific account mid-migration? Click the **⏸ Pause** button on that row.
   * Pausing an account immediately cancels its active network transfer and releases its worker slot so other queued accounts can proceed.
   * Click **▶ Resume** at any time to re-queue the account into the active batch without resetting overall progress.
3. **Batch Re-Run (v1.4.0):**
   * If you need to perform a delta catch-up sync after DNS cutover, simply click **▶ Start Batch Migration** again.
   * All account progress bars, speeds, and counters automatically reset cleanly, and the deduplication engine skips previously transferred messages in seconds.
4. **Native Sleep Prevention:**
   * MailMigrator automatically prevents the computer from sleeping during active batch migrations, ensuring unattended overnight transfers complete reliably.
