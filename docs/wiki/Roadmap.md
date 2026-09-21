# Planned Features & Ideas Backlog

This document outlines features, architectural ideas, and community requests currently being investigated for upcoming updates to **Mundofy MailMigrator**, as well as recently shipped enhancements.

These backlog items are not tied to rigid milestone dates—they represent our active ideas queue. Features are prioritized based on community demand, developer bandwidth, and real-world sysadmin feedback.

Have an idea or need a specific feature? Feel free to open a suggestion on our [GitHub Issue Tracker](https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues)!

---

## 💡 High-Priority Features Under Investigation

### ☁️ Google Workspace & Microsoft 365 to Nextcloud Migration
* **Concept:** Direct, end-to-end migration pipeline from proprietary big-tech ecosystems (Google Workspace and Microsoft 365) to private, sovereign **Nextcloud** instances.
* **Scope & Capabilities:**
  * **Email & Mailbox Transfer:** Seamless modern OAuth2 / IMAP transfer into Nextcloud's underlying mail backend (Dovecot, Stalwart, Mailcow, Postfix) with automatic folder structure alignment.
  * **Nextcloud Provisioning & Auto-Configuration:** Auto-detection and direct connection via Nextcloud Provisioning API (OCS) and Nextcloud Mail app endpoints.
  * **Groupware Expansion (Contacts & Calendars):** Automated migration of address books (Google Contacts / Outlook People) to Nextcloud Contacts via CardDAV, and events (Google Calendar / Outlook Calendar) to Nextcloud Calendar via CalDAV.
  * **Drive to Nextcloud Files (WebDAV):** Optional data and document migration from Google Drive / OneDrive into user Nextcloud storage directories.
* **Benefit:** Empowers organizations, enterprises, and privacy-conscious users with a 1-click sovereign exit route from Google Workspace and Microsoft 365 to private Nextcloud infrastructure with zero vendor lock-in.

### 🔄 Smart Folder Normalization & Mapping
* **Concept:** Automatic translation and mapping between different server folder naming conventions.
* **Benefit:** Prevents duplicate system folders on the destination (e.g., mapping Outlook's `Sent Items` directly into cPanel's `Sent`, or mapping localized folder names like `Gelöschte Elemente` to `Trash`).

### 📊 Exportable HTML Migration Audit Reports
* **Concept:** A one-click generation of comprehensive, styled HTML summary reports alongside the existing CSV and TXT log exports.
* **Benefit:** Generates a professional proof-of-work report (accounts, folders, messages copied vs. skipped, speed, and any error logs) ideal for IT consultancies and managed service providers delivering migration sign-offs to clients.

### 🔁 1-Click "Retry Failed Only"
* **Concept:** In a batch migration with dozens of mailboxes, if any accounts fail (e.g. due to bad credentials or transient server dropouts), a dedicated button will instantly filter and re-run only the failed accounts.
* **Benefit:** Saves time by not needing to re-evaluate the successful mailboxes in the batch.

### ⚡ Bandwidth Throttling & Rate Limiting
* **Concept:** An optional slider to limit maximum download/upload speed (e.g. limit to 10 MB/s) or pause briefly between accounts.
* **Benefit:** Prevents aggressive ISP bandwidth saturation or mail server rate-limiting bans on shared connections.

### 📅 Date Range Filtering
* **Concept:** Give users the option to migrate only emails received after a certain date (e.g. *"last 6 months"*, *"last 1 year"* or a custom start and end date).
* **Benefit:** Very helpful when migrating users with strict mailbox size limits or who only need recent correspondence transferred to a new cloud mailbox.

### 🗑️ Selective Folder Exclusion (Skip Trash & Spam)
* **Concept:** Quick-toggle options to skip `Trash`, `Deleted Items`, `Junk`, and `Spam` folders during migration, plus custom wildcard exclusion rules (e.g. `Archive/2010*`).
* **Benefit:** Drastically reduces transfer time and bandwidth by skipping unwanted emails.

### 🐧 Cross-Platform Linux & Docker CLI
* **Concept:** A lightweight, standalone cross-platform console build (`linux-x64`, `linux-arm64`) and an official Docker container image (`docker run mundofy/mailmigrator ...`).
* **Heritage & Motivation:** The classic `imapcopy` was our core inspiration and a trusted tool for Linux sysadmins for over a decade. Bringing modern TLS 1.3 encryption, modern 64-bit architecture, POP3 support, and containerized deployment back to Linux will give sysadmins a high-speed modern successor.
* **Benefit:** Enables automated, headless migrations on Linux VPS servers, Kubernetes clusters, and container pipelines with zero dependencies.

---

## ✅ Recently Shipped Features

| Feature | Version | Description |
| :--- | :--- | :--- |
| **Headless CLI File Logging & SSL Bypass** | `v1.6.0` | CLI real-time file logging (`-l`), unsigned/self-signed SSL bypass (`-k`), `LogFile` and `AllowInvalidCertificates` config directives, headless sleep prevention, and modernized `Dist/ImapCopy.cfg`. |
| **Microsoft 365 & Google Modern OAuth2** | `v1.5.0` | Native browser-based OAuth2 sign-in with PKCE and SASL XOAUTH2 token pipeline for single accounts. |
| **Enterprise Batch OAuth2** | `v1.5.0` | Tenant-wide passwordless migrations via Azure App Registration (`IMAP.AccessAsApp`) and Google Service Account JSON keys with Domain-Wide Delegation. |
| **Universal macOS Application** | `v1.5.0` | Dedicated Avalonia UI 12 macOS application released as Universal DMG and app zip for Apple Silicon (M1-M4) and Intel Macs. |
| **Dynamic Protocol Columns & Smart CSV** | `v1.5.2` | Context-sensitive batch table headers (`Admin OAuth2` badges) and multi-format CSV auto-detection (1-column, 2-column, 4-column). |
| **Mailbox Quota Checker & Capacity Guard** | `v1.4.0` | RFC 2087 IMAP QUOTA metrics, POP3 sizing, color-coded health badges, and automated pre-flight destination capacity validation. |
| **Batch Re-Run Reset** | `v1.4.0` | Clean counter and progress reset when re-running completed batches for delta catch-up syncs. |
| **Dedicated Privacy & Compliance UI** | `v1.4.0` | Native in-app tab detailing zero-telemetry guarantee and global privacy compliance (GDPR/CCPA/HIPAA). |
| **Native System Sleep Prevention** | `v1.3.0` | Windows power management integration (`SetThreadExecutionState`) preventing PC sleep during active migrations while allowing screens to power off. |
| **Per-Account Batch Pause & Resume** | `v1.3.0` | Row-level pause/resume controls releasing and reclaiming concurrency slots dynamically. |
| **Live Log Search & Dual-Format Export** | `v1.2.2` | Real-time log search filtering with match counters and dual `.txt` / `.csv` exporting. |
| **Zero-Config Server Auto-Discovery** | `v1.0.0` | 5-tier resolution cascade querying DNS SRV, MX fingerprinting, Mozilla ISPDB, and cPanel/Plesk autoconfig. |
