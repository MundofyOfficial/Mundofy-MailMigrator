# Batch Migration & Concurrency Guide

The **Batch Migration** tab enables migrating dozens or hundreds of mailboxes concurrently in a single automated session.

---

## ⚡ Concurrency & Worker Scaling

Mundofy MailMigrator includes an adaptive worker engine:
* **Worker Slider:** Allows adjusting parallel workers from **1 to 16 threads** (default: 4).
* **Throughput Optimization:**
  * **1–2 Workers:** Ideal for metered or rate-limited connections.
  * **4–8 Workers:** Ideal for high-speed fiber connections migrating standard corporate domains.
  * **8–16 Workers:** Recommended for high-bandwidth dedicated servers migrating large email migrations.

---

## 📋 Adding Accounts to the Batch

You can populate the account migration table through four convenient methods:

### 1. Direct Table Editing
* Click **➕ Add Account** to insert a new blank row into the DataGrid.
* Double-click any cell to enter source username, source password, destination username, destination password, or custom server overrides.

### 2. Clipboard Paste (Excel & Tab-Separated)
* Copy columns directly from Microsoft Excel, Google Sheets, or a text editor in the format:
  `	sv
  src_user	src_pass	dst_user	dst_pass
  `
* Click **📋 Paste Clipboard** to instantly populate all rows.

### 3. Importing ImapCopy.cfg
* Click **📂 Load ImapCopy.cfg** to import existing configuration files from the classic imapcopy tool.
* Server hostnames, ports, flags, and account pairs are automatically loaded into the UI.

### 4. Importing CSV / Spreadsheets
* Click **📄 Import CSV** to import .csv files formatted with headers:
  SourceUser,SourcePassword,DestUser,DestPassword

---

## 🔍 Testing Credentials in Parallel

Before executing a large batch:
1. Click **🔍 Test All Credentials**.
2. The orchestrator tests logins for both source and destination servers in parallel.
3. Status indicators in the table turn green (**Ready**) or red (**Auth Failed**), allowing you to fix incorrect passwords before beginning the copy operation.

---

## ▶ Executing the Batch

1. Click **▶ Start Batch Migration**.
2. Accounts are processed according to the configured worker count.
3. As each mailbox completes:
   * Status column updates to **Completed**.
   * Transferred message counts and errors are tallied in real time.
4. If an individual account experiences network drops or server timeouts, it is isolated so other migrations continue uninterrupted.
