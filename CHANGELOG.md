# Changelog

All notable changes to **Mundofy MailMigrator** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] - 2026-09-20

### Added
* **Microsoft 365 Modern Authentication (OAuth2 / XOAUTH2)**:
  * Native browser-based interactive sign-in flow supporting modern OAuth2 and PKCE for Microsoft 365 (Office 365 / Outlook) without legacy basic authentication.
  * Direct IMAP SASL `XOAUTH2` authentication via secure MailKit token pipeline.
  * Interactive "🔑 Sign In" button with real-time account badge and quick token clear controls on Single Account Migration (Source and Destination).
  * Auto-configuration of Microsoft endpoint host (`outlook.office365.com`) and secure SSL port (993).
* **Google Workspace Modern Authentication (OAuth2 / XOAUTH2)**:
  * Native browser-based loopback OAuth2 sign-in flow with PKCE for Google Workspace and Gmail accounts.
  * Direct IMAP SASL `XOAUTH2` authentication over SSL.
  * Interactive "🔑 Sign In" button with connected account status badge and clear controls on Single Account Migration (Source and Destination).
  * Auto-configuration of Google endpoint host (`imap.gmail.com`) and secure SSL port (993).
* **Enterprise Batch Migration for Microsoft 365**:
  * Tenant-wide migration without individual mailbox passwords using Azure App Registration (`IMAP.AccessAsApp` application permissions).
  * Dedicated enterprise configuration fields: Tenant ID (Directory ID), Application (Client) ID, and Client Secret.
  * Dynamic per-mailbox OAuth2 bearer token acquisition via MSAL client credentials flow for batch accounts.
* **Enterprise Batch Migration for Google Workspace**:
  * Domain-wide batch migration without individual user passwords using Google Cloud Service Account JSON keys with Domain-Wide Delegation.
  * Interactive file browser to load Google Service Account key files (`.json`) with automated client credential parsing.
  * Dynamic per-mailbox token assertion with user subject impersonation (`https://mail.google.com/` scope) for batch accounts.
* **Full Cross-Platform Parity**:
  * Both Windows (WPF) and macOS (Avalonia) desktop applications feature identical Microsoft 365 and Google Workspace modern authentication and enterprise batch options.

---

## [1.4.0] - 2026-09-19

### Added
* **Dedicated Privacy & Compliance Tab & Zero-Telemetry Guarantee**:
  * Native desktop UI tab detailing 100% standalone, client-side architecture with point-to-point TLS 1.3 socket streaming and zero cloud relays.
  * Comprehensive worldwide privacy compliance breakdown (GDPR, CCPA/CPRA, PIPEDA, HIPAA-ready).
  * Complete transparency documentation detailing the single anonymous GitHub API update check.
  * Direct 1-click transition button from the About screen to the Privacy & Compliance tab.
* **IMAP & POP3 Mailbox Quota Checker**:
  * Added RFC 2087 IMAP QUOTA protocol support, converting 1024-octet storage blocks into accurate byte metrics (MB / GB) and percentage utilization.
  * Added POP3 storage usage calculation using message sizing and count metrics.
  * Interactive "📊 Check Quotas" batch toolbar button and right-click context menu item to query mailbox limits and usage for all accounts in parallel.
  * Real-time color-coded health badges (Normal, Warning >= 80%, Critical >= 95%, Exceeded >= 100%, and Unlimited).
  * Storage quota indicators on Single Account Migration for both Source and Destination mailboxes.
  * Detection and clean formatting for unmetered and unlimited storage mailboxes.
* **Pre-Flight Destination Storage Capacity Guard**:
  * Automated pre-flight comparison between source mailbox size and destination available free space.
  * Warns the operator before migration begins if the destination mailbox has insufficient capacity, preventing destination server `[OVERQUOTA]` rejections.
  * Supported on both Single Account Migration and Multi-Account Batch Migration.
* **Centralized Version Source of Truth**:
  * Centralized `AppVersion` constant in `Mundofy.MailMigrator.Core` with dynamic assembly metadata reflection.
  * Dynamically bound header version badge, About tab version text, activity startup logs, and User-Agent headers.

### Fixed
* **Batch Re-Run & Fresh Counter Reset**:
  * Pressing "Start Batch Migration" when all accounts were previously completed now cleanly re-queues all accounts as a fresh run instead of doing nothing.
  * Automatically resets message counters (`TotalMessages`, `CopiedMessages`, `SkippedMessages`, `FailedMessages`), transfer speed, and row progress bars to `0% / Queued`.
  * Instantly resets the overall batch progress bar and status text upon clicking start, providing clear visual feedback that a new request has begun.

---

## [1.3.0] - 2026-09-19

### Added
* **Automatic Computer Sleep Prevention During Migration**:
  * Integrates Windows native power management (`SetThreadExecutionState` with `ES_CONTINUOUS | ES_SYSTEM_REQUIRED`) to prevent the PC from entering idle sleep or standby while migrations are actively in progress.
  * Preserves active TCP sockets and network throughput during long/overnight runs without preventing monitors from powering down to save energy.
  * Fully configurable in the Settings & Logs tab ("Prevent Computer Sleep During Migration") and enabled by default with `%APPDATA%` persistence.
* **Per-Account Pause & Resume in Multi-Batch Migration**:
  * Direct row-level pause and resume controls in the active batch table.
  * Pausing an individual account immediately cancels its active transfer, releases its worker slot, and leaves other concurrently migrating accounts completely unaffected.
  * One-click resume re-queues the account into the active batch without resetting total batch counts.
* **Protocol Selection on Batch Migration Tab**:
  * Added source protocol selection supporting both IMAP and POP3 engines for batch accounts.
  * Added "Coming Soon" indicators for Microsoft 365 (Graph API) and Google Workspace (Gmail API) with informative tooltips and custom disabled item styling.
* **Dynamic Control Locking During Active Migration**:
  * Protocol selectors, host/port textboxes, and concurrency slider automatically disable and dim while a migration is actively running, preventing invalid mid-transfer modifications.
* **Precision Vector Dialog Icons**:
  * Replaced DirectWrite emoji glyphs in notification dialogs with mathematically centered vector icons (`Warning`, `Error`, `Question`, `Info`), eliminating font baseline shifts and Windows 11 emoji palette color mismatches.

---

## [1.2.2] - 2026-09-18

### Added
* **Live Activity Log Search and Real-Time Filtering**:
  * Added dynamic search filtering to the Live Activity Log toolbar, matching log messages, status levels, or timestamps in real time.
  * Added entry counter displaying matching and total record counts.
  * Added quick-clear search button.
* **Dual-Format Log Export (.TXT and .CSV)**:
  * Export activity logs to standard plaintext log files (`.txt`) or structured comma-separated values (`.csv`) with automatic header generation and RFC-compliant escaping.
  * Supports exporting filtered log results (e.g., exporting only errors or warnings).

### Changed
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

## [1.2.1] - 2026-09-18

### Added
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

---

## [1.2.0] - 2026-09-18

### Added
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

### Changed
* **Single Executable Distribution**:
  * Discontinued duplicate versioned executable generation. Releases package and distribute exclusively `MundofyMailMigrator.exe` to ensure stable shortcut targets and automated deployment compatibility.

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