# Changelog

All notable changes to **Mundofy MailMigrator** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2026-09-17

### Added
* **⚡ Zero-Config Server Auto-Discovery**:
  * Automatically detects incoming IMAP host, port (993), and SSL encryption from any email address or domain.
  * **5-Tier Discovery Cascade**:
    1. *Major Provider Directory:* Instant 0ms mapping for Gmail, Google Workspace, Microsoft 365, Outlook, Yahoo, iCloud, Fastmail, Zoho, GMX, Web.de, and Yandex.
    2. *RFC 6186 DNS SRV Records:* Resolves `_imaps._tcp` (port 993) and `_imap._tcp` (port 143) to identify custom corporate email hosts.
    3. *DNS MX Fingerprinting & Socket Handshakes:* Identifies Google Workspace / Microsoft 365 on custom domains, or actively probes custom MX mail hosts via SSL handshake.
    4. *Mozilla Thunderbird ISPDB:* Queries `autoconfig.thunderbird.net` for global ISP profiles.
    5. *Domain Autoconfig XML:* Probes cPanel, Plesk, and DirectAdmin endpoints.
  * **Strict Verification:** Eliminates false assumptions; unverified domains are left blank for manual input with clear feedback.
  * **Dedicated UI Buttons:** Added `⚡ Auto-Detect` to Source and Destination cards on both Single and Batch tabs.
  * **Reactive Input:** Auto-discovery gently triggers when a valid email address is typed into an empty server field.

* **⚡ In-Flight Account Queueing & Dynamic Batch Execution**:
  * Add new email accounts during an active batch migration without stopping or restarting the process.
  * Added **`▶`** button directly on every row to immediately queue a newly typed account into the running stream.
  * Added instant queueing when pasting accounts from clipboard or importing CSV while a migration is in progress.
  * Converted batch processing engine to an unbounded multi-worker channel with automated completion debounce.

* **🎨 Modernized Dark-Mode Context Menu & Row Actions**:
  * Redesigned right-click menu with native dark styling (`#1E293B`), 8px rounded corners, elevation drop shadows, and blue highlight states.
  * Fixed right-click behavior so the row under the cursor is automatically selected and targeted.
  * Added context menu quick actions: **`▶ Start / Queue This Account`**, **`🔍 Test Account Credentials`**, **`📋 Copy Source/Dest Email`**, **`🗑 Remove Account`**, **`🧹 Clear Completed Accounts`**, and **`⚠️ Clear Entire Batch`**.
  * Added dual-action column with side-by-side **`▶`** (Start/Queue) and **`🗑`** (Delete) buttons on each row.

* **✨ Custom Dark Modal Dialogs & Fixed DataGrid Selection**:
  * **Eliminated the "White Line" Selection Bug:** Replaced default Win32/Aero system selection brushes with custom transparent cell templates and rich deep navy (`#1E3A5F`) row selection highlighting. Text columns and cells now maintain a unified, dark slate look without white backgrounds.
  * **Custom Dark Import Dialog:** Replaced native Win32 `MessageBox` with a custom `ImportAccountsDialog` matching the app's aesthetic: dark slate card with drop shadows, live account count comparison badges, clear descriptions, and custom buttons (**`🔄 Replace List`**, **`➕ Append to List`**, and **`Cancel`**).
  * **Universal Dark Alerts:** Added `DarkMessageBox` across the entire app for warnings, errors, and queue confirmations, completely removing all standard white Windows message boxes.

* **📦 Versioned Executable Distribution**:
  * Added explicitly named standalone binary: `MundofyMailMigrator-v1.1.0.exe` alongside `MundofyMailMigrator.exe` to prevent browser caching conflicts.

* **🛡️ Multi-Tier Robust Deduplication Engine**:
  * Eliminates duplicate email transfers on incremental runs, even when IMAP servers format headers differently or omit standard RFC `Message-ID` fields.
  * **3-Tier Protection Hierarchy**:
    1. *Normalized RFC 5322 Message-ID:* Trims enclosing angle brackets (`<`, `>`), strips whitespace, and compares case-insensitively to prevent cross-server envelope formatting mismatches.
    2. *Composite Fallback Fingerprinting:* Fallback for messages lacking a `Message-ID` header (or where the server returns `NIL` in `FETCH ENVELOPE`). Constructs a deterministic composite signature combining UTC Timestamp (to the minute), Sender (`From`), and Subject line.
    3. *Synthetic Message-ID Injection:* When copying an email without an RFC `Message-ID`, dynamically injects a deterministic synthetic header (`<synth-{hash}@mundofy.migrated>`) into the destination message so subsequent sync passes identify it as an existing message immediately.
  * **Unified Pipelines:** Applied across both IMAP-to-IMAP and POP3-to-IMAP migration engines.
  * **Live Diagnostic Logging:** Logs exact message subject, date, and assigned Message-ID/fingerprint for all copied messages.

---

## [1.0.0] - 2026-09-16

### Initial Release
* High-speed IMAP and POP3 email migration suite for Windows (.NET 8).
* Native TLS 1.3 encryption out-of-the-box (no `stunnel` required).
* Dual interface: Interactive Dark-Mode WPF Desktop GUI & Headless CLI (`-c ImapCopy.cfg`).
* Full backward compatibility with classic `ImapCopy.cfg` directives and files.
* Concurrent multi-worker batch processing (1 to 16 parallel threads).
* Lossless RFC 822 `Message-ID` deduplication with live resume capability.
* Real-time metrics: transfer speed in MB/s, folder progress, error counters.
* CSV and TSV spreadsheet import, export, and clipboard pasting.
* Standalone single-file executable (`publish\win-x64\MundofyMailMigrator.exe`).
