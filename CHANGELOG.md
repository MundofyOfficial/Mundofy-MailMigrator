# Changelog

All notable changes to **Mundofy MailMigrator** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2026-09-18

### Added
* **Per-Account Pause & Resume in Multi-Batch Migration**:
  * Direct row-level pause and resume controls in the active batch table.
  * Pausing an individual account immediately cancels its active transfer, releases its worker slot, and leaves other concurrently migrating accounts completely unaffected.
  * One-click resume re-queues the account into the active batch without resetting total batch counts.
* **Protocol Selection on Batch Migration Tab**:
  * Added source protocol selection supporting both IMAP and POP3 engines for batch accounts.
  * Added "Coming Soon" indicators for Microsoft 365 (Graph API) and Google Workspace (Gmail API) with informative tooltips and custom disabled item styling.
* **Precision Vector Dialog Icons**:
  * Replaced DirectWrite emoji glyphs in notification dialogs with mathematically centered vector icons (`Warning`, `Error`, `Question`, `Info`), eliminating font baseline shifts and Windows 11 emoji palette color mismatches.
* **Date Range Filtering (DD-MM-YYYY)**:
  * Restrict message transfers by date envelope (since a specified start date, before an end date, or within a bounded date window).
  * High-efficiency early filtering: inspects envelope metadata during the initial folder scan and skips excluded messages before downloading message bodies.
  * Standard presets: All Emails (No date limit), Last 6 Months, Last 1 Year, Last 2 Years, and Custom Date Range.
  * European DD-MM-YYYY format support with input validation and inline diagnostic feedback.
  * Unified support across both IMAP-to-IMAP and POP3-to-IMAP migration engines.
* **In-App Update Checker and In-Place Self-Updater**:
  * Non-blocking background verification of new GitHub releases on application startup.
  * Interactive "Check for Updates" control in the Settings panel.
  * Header notification badge indicating available updates.
  * Modal dialog detailing the latest version number, release date, and changelog.
  * One-click in-place executable replacement and restart via detached process, maintaining existing filesystem paths and shortcuts without requiring an external installer.
* **Input and Session State Persistence**:
  * Automatically saves and restores last-used server hostnames, ports, protocols, SSL configurations, and usernames across application restarts in `%APPDATA%\Mundofy\MailMigrator\settings.json`.
  * Persists advanced migration preferences (deduplication, certificate allowances, folder exclusions, and date filter configurations).
  * Explicitly excludes passwords and batch account lists from disk storage to maintain credential security.
* **Password Privacy Masking Toggle**:
  * Added password visibility toggle in the batch migration toolbar (`Show Passwords` / `Hide Passwords`).
  * Masks source and destination passwords with bullets in the batch accounts table by default.
  * Supports full inline cell editing and clipboard pasting while maintaining on-screen privacy.
* **Invalid SSL Certificate Warning**:
  * Added interactive security confirmation modal before initiating single or batch migrations when "Permit Self-Signed / Invalid SSL Certificates" is enabled, warning users of Man-in-the-Middle (MitM) credential interception risks.
* **Live Activity Log Search and Real-Time Filtering**:
  * Added dynamic search filtering to the Live Activity Log toolbar, matching log messages, status levels, or timestamps in real time.
  * Added entry counter displaying matching and total record counts.
  * Added quick-clear search button.
* **Dual-Format Log Export (.TXT and .CSV)**:
  * Export activity logs to standard plaintext log files (`.txt`) or structured comma-separated values (`.csv`) with automatic header generation and RFC-compliant escaping.
  * Supports exporting filtered log results (e.g., exporting only errors or warnings).

### Changed
* **Single Executable Distribution**:
  * Discontinued duplicate versioned executable generation. Releases package and distribute exclusively `MundofyMailMigrator.exe` to ensure stable shortcut targets and automated deployment compatibility.
* **Enlarged Activity Log Interface and Smooth Vertical Scrolling**:
  * Expanded the log view height to 460px with automatic vertical scrolling across Settings & Logs and About screens.
  * Implemented smooth mouse wheel event bubbling between log list and outer page container.

### Fixed
* **Application-Wide Dark ToolTip Styling**:
  * Added global dark theme ToolTip style in `App.xaml`, eliminating Windows Aero white/light-gray tooltips in favor of rounded dark slate containers with high-contrast text.
* **Update Notification Button Mouseover**:
  * Implemented custom ControlTemplate with defined dark emerald hover and pressed states, preventing default Windows Aero light-cyan wash and preserving text legibility.
* **ScrollViewer Intersection Corner**:
  * Replaced default Windows white corner square where horizontal and vertical scrollbars intersect with a transparent container and dark background override.

---

## [1.1.0] - 2026-09-17

### Added
* **Zero-Config Server Auto-Discovery**:
  * Automatically resolves incoming IMAP host, port (993), and SSL encryption from any email address or domain.
  * Five-tier discovery cascade:
    1. Major Provider Directory: Instant mapping for Google Workspace, Microsoft 365, Outlook, Yahoo, iCloud, Fastmail, Zoho, GMX, Web.de, and Yandex.
    2. RFC 6186 DNS SRV Records: Queries `_imaps._tcp` (port 993) and `_imap._tcp` (port 143) to resolve corporate email endpoints.
    3. DNS MX Fingerprinting and Socket Handshakes: Identifies hosted enterprise services or probes custom mail exchange hosts via direct SSL handshakes.
    4. Mozilla Thunderbird ISPDB: Queries autoconfig.thunderbird.net for global ISP connection profiles.
    5. Domain Autoconfig XML: Probes standard hosting control panel endpoints (cPanel, Plesk, DirectAdmin).
  * Strict Verification: Prevents unwarranted assumptions; unverified domains remain blank for explicit manual configuration.
  * Interactive UI controls: Added Auto-Detect buttons for source and destination hosts across Single and Batch tabs.
  * Reactive input: Initiates discovery when a valid email address is entered into an empty server field.
* **In-Flight Account Queueing and Dynamic Batch Execution**:
  * Allows adding accounts to an active batch migration without stopping or restarting the running process.
  * Individual row controls to immediately queue newly added accounts into the running execution stream.
  * Dynamic queueing support for clipboard paste and CSV file imports during an active run.
  * Re-architected batch processing engine using unbounded multi-worker channels with automated completion debouncing.
* **Redesigned Context Menu and Row Actions**:
  * Native dark styling with custom border radiuses, elevation drop shadows, and high-contrast highlight states.
  * Context menu auto-selection ensures right-clicking anywhere on a row immediately selects the target account.
  * Quick actions: Start / Queue Account, Test Account Credentials, Copy Source/Dest Email, Remove Account, Clear Completed Accounts, and Clear Entire Batch.
  * Dual-action column providing dedicated inline buttons for individual account execution and removal.
* **Modal Dialog Architecture and DataGrid Styling**:
  * Eliminated system selection artifacts: Replaced default Win32/Aero selection brushes with custom transparent cell templates and dark navy row selection highlighting.
  * Custom Dark Import Dialog: Replaced standard Win32 alert with `ImportAccountsDialog`, featuring structured account statistics, operational descriptions, and explicit selection controls (Replace List, Append to List, Cancel).
  * Universal dark alert dialogs: Integrated `DarkMessageBox` across the application, removing standard Win32 prompt dialogs.
* **Multi-Tier Message Deduplication Engine**:
  * Prevents duplicate message transfers during incremental runs when headers differ across IMAP implementations.
  * Three-tier identification hierarchy:
    1. Normalized RFC 5322 Message-ID: Trims enclosing brackets, strips whitespace, and performs case-insensitive comparisons.
    2. Composite Fallback Fingerprinting: Generates a deterministic signature using UTC timestamp, sender address, and subject line when Message-ID is missing or returned as NIL.
    3. Synthetic Message-ID Injection: Generates and embeds a deterministic synthetic header (`<synth-{hash}@mundofy.migrated>`) into the destination message when transferring messages that lack RFC Message-IDs, ensuring subsequent synchronization passes detect existing items immediately.
  * Unified across IMAP and POP3 migration engines.
  * Diagnostic logging of message subject, timestamp, and identifier per transferred item.

---

## [1.0.0] - 2026-09-16

### Initial Release
* High-speed IMAP and POP3 email migration suite for Windows (.NET 8).
* Native TLS 1.3 encryption support without external proxy dependencies.
* Dual interface: Interactive Dark-Mode WPF Desktop GUI and Headless CLI (`-c ImapCopy.cfg`).
* Full backward compatibility with classic `ImapCopy.cfg` directives and configurations.
* Concurrent multi-worker batch processing (configurable from 1 to 16 parallel threads).
* Lossless RFC 822 `Message-ID` deduplication with incremental resume capability.
* Real-time metrics: transfer speed in MB/s, folder progress, and error counters.
* CSV and TSV spreadsheet import, export, and clipboard parsing.
* Standalone single-file executable distribution.
