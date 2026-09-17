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

* **🗑️ Batch Migration Row Management**:
  * Added **`➖ Remove Selected`** button on the Batch Migration toolbar to delete selected rows without clearing the entire table.
  * Added a dedicated **Action** column with a **`🗑`** button on every row for 1-click row deletion.
  * Added native keyboard **`Delete`** key support on DataGrid rows.
  * Added right-click **Context Menu** on the accounts table with *"Remove Selected Account"* and *"Clear Entire Batch"*.

* **📦 Versioned Executable Distribution**:
  * Added explicitly named standalone binary: `MundofyMailMigrator-v1.1.0.exe` alongside `MundofyMailMigrator.exe` to prevent browser caching conflicts.

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
