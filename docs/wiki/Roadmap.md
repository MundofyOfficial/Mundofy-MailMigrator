# Planned Features & Ideas Backlog

This document outlines features, architectural ideas, and community requests currently being investigated for upcoming updates to **Mundofy MailMigrator**.

These items are not tied to rigid milestone dates or version numbers—they represent our active ideas backlog. Features may be implemented based on community demand, developer bandwidth, or user feedback.

Have an idea or need a specific feature? Feel free to open a suggestion on our [GitHub Issue Tracker](https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues)!

---

## 💡 High-Priority Features Under Investigation

### 📅 Date Range Filtering
* **Concept:** Give users the option to migrate only emails received after a certain date (e.g. *"last 6 months"*, *"last 1 year"* or a custom start and end date).
* **Benefit:** Very helpful when migrating users with strict mailbox size limits or who only need recent correspondence transferred to a new cloud mailbox.

### 🗑️ Selective Folder Exclusion (Skip Trash & Spam)
* **Concept:** Quick-toggle options to skip `Trash`, `Deleted Items`, `Junk`, and `Spam` folders during migration, plus custom wildcard exclusion rules (e.g. `Archive/2010*`).
* **Benefit:** Drastically reduces transfer time and internet bandwidth by avoiding migrating gigabytes of unwanted emails.

### 🔄 Smart Folder Normalization & Mapping
* **Concept:** Automatic translation and mapping between different server folder naming schemes.
* **Benefit:** Prevents duplicate system folders on the destination (e.g., mapping Outlook's `Sent Items` directly into cPanel's `Sent`, or mapping localized folder names like `Gelöschte Elemente` to `Trash`).

### 📊 Exportable Migration Audit Reports (CSV & HTML)
* **Concept:** A one-click button in both the Desktop GUI and CLI to export a complete post-migration audit log.
* **Benefit:** Generates a detailed proof-of-work report (accounts, folders, messages copied vs. skipped, speed, and any error logs) ideal for IT administrators managing client migrations.

### 🔁 1-Click "Retry Failed Only"
* **Concept:** In a batch migration with dozens of mailboxes, if any accounts fail (e.g. due to bad passwords or transient network timeouts), a dedicated button will instantly filter and re-run only the failed accounts.
* **Benefit:** Saves time by not needing to re-evaluate the successful mailboxes in the DataGrid.

### ⚡ Bandwidth Throttling & Rate Limiting
* **Concept:** An optional slider to limit maximum download/upload speed (e.g. limit to 10 MB/s) or pause briefly between accounts.
* **Benefit:** Prevents aggressive ISP bandwidth saturation or mail server rate-limiting bans on shared connections.

### 🔐 OAuth 2.0 / Modern Authentication
* **Concept:** Interactive browser login flow for modern cloud email services (Microsoft 365 and Google Workspace).
* **Benefit:** Allows connecting without requiring users or domain admins to configure 16-character App Passwords or disable MFA.

### 🐧 Cross-Platform Linux & Docker CLI
* **Concept:** A standalone cross-platform console build (`linux-x64`, `linux-arm64`) and an official Docker container image (`docker run mundofy/mailmigrator ...`).
* **Heritage & Motivation:** The classic `imapcopy` by Armin Diehl was our core inspiration and a trusted tool for Linux sysadmins for over a decade. However, it is unfortunately getting outdated (lacking native TLS 1.3 encryption, modern 64-bit architecture, POP3 support, and containerized deployment). Revitalizing this legacy with a native Linux binary and official Docker image will give Linux sysadmins a modern, high-speed successor.
* **Benefit:** Enables automated, headless migrations on Linux VPS servers, Kubernetes clusters, and container pipelines with zero dependencies.
