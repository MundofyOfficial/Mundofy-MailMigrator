# Welcome to the Mundofy MailMigrator Wiki

**Mundofy MailMigrator** is a modern, high-speed email migration suite engineered for Windows and macOS, supporting both an intuitive desktop GUI and a headless command-line interface (CLI). It enables fast, secure, and lossless mailbox migrations between IMAP, POP3, Microsoft 365, and Google Workspace email servers with native TLS 1.3 encryption, modern OAuth2 authentication, concurrent multi-threading, mailbox capacity guards, and intelligent RFC 822 Message-ID deduplication.

---

## 💡 Why This Tool Was Created

As a managed hosting provider, we routinely help customers migrate their mailboxes over to our servers. Migrating email can be technically intimidating, and most users simply don't have the specialized knowledge or command-line experience to do it themselves safely without risking lost messages or downtime.

For years, the go-to tool for this job was the classic open-source **`imapcopy`** by Armin Diehl. It was simple, reliable, and sysadmins everywhere swore by it.

Over time, however, email infrastructure evolved. Mail servers began dropping older protocols and strictly enforcing **TLS 1.3**. Because classic `imapcopy` hasn't been maintained in years, connection handshakes started failing. Keeping it running required clumsy workarounds like running local `stunnel` proxy daemons, and it still lacked support for POP3, multi-threading, modern OAuth2, or modern error handling.

Patching an outdated utility was no longer the smartest way forward. So with an open mind, we asked a simple question:

> *"What if we create a modern tool that does everything imapcopy did, but so much more?"*

That idea became **Mundofy MailMigrator**:
* **Native TLS 1.3 & SSL out-of-the-box** — zero proxies or wrappers needed.
* **⚡ Zero-Config Server Auto-Discovery** — automatically resolves IMAP host, port, and SSL from any email address.
* **🔑 Modern OAuth2 & SASL XOAUTH2** — 1-click browser sign-in for Microsoft 365 and Google Workspace, plus enterprise tenant/domain batching without individual passwords.
* **📊 Mailbox Quota Checker & Pre-Flight Capacity Guard** — live RFC 2087 IMAP & POP3 storage monitoring and automated prevention of `[OVERQUOTA]` failures.
* **100% backward compatibility** with classic `ImapCopy.cfg` configuration files.
* **POP3 source support** for migrating older mailboxes that don't have IMAP enabled.
* **Concurrent multi-account batching** with an intuitive Desktop GUI alongside the CLI.
* **Smart RFC 822 Message-ID deduplication** for lossless, resume-friendly transfers.
* **Zero-Telemetry & 100% Privacy by Design** — direct point-to-point in-memory streaming with no cloud relays.

Rather than keeping this as an internal utility, we open-sourced it for everyone. Whether you're a sysadmin managing domain migrations or someone moving an individual inbox, this tool was built to make email migration painless—and with the help of the open-source community, our goal is to make this the modern, free go-to tool for everyone.

---

## 📖 Wiki Table of Contents

| Section | Description |
| :--- | :--- |
| **[👤 Single Account Migration](Single-Account-Migration)** | Step-by-step guide to migrating an individual mailbox between IMAP, POP3, M365, or Google Workspace. |
| **[👥 Batch Migration & Concurrency](Batch-Migration-&-Multi-Threading)** | Managing enterprise migrations with parallel workers, spreadsheet imports, and DataGrid editing. |
| **[🔑 Modern OAuth2 & Cloud Providers](Modern-OAuth2-&-Cloud-Providers)** | Interactive sign-in, Azure App Registration setup, and Google Service Account Domain-Wide Delegation. |
| **[📊 Mailbox Quotas & Capacity Guard](Mailbox-Quotas-&-Capacity-Guard)** | Real-time storage metrics, parallel quota checks, and pre-flight over-quota protection. |
| **[🍎 macOS User Guide](macOS-User-Guide)** | Running the native Universal macOS application (DMG/App) on Apple Silicon and Intel Macs. |
| **[💻 CLI & Headless Automation](CLI-&-Headless-Automation)** | Scripting migrations in CI/CD, scheduled tasks, and headless server environments. |
| **[📄 Configuration File Format](Configuration-File-Format)** | Deep dive into legacy ImapCopy.cfg syntax, directives, and multi-format CSV/TSV table formats. |
| **[🔄 Smart Deduplication & Sync](Smart-Deduplication-&-Sync)** | Understanding how zero-duplicate transfers and incremental resumption work. |
| **[📝 Live Logs, Search & Export](Logs-Search-&-Export)** | Real-time log filtering, TXT/CSV exporting, and native PC sleep prevention. |
| **[🔒 Privacy, Security & Compliance](Privacy-and-Security)** | Standalone architecture, GDPR/CCPA/HIPAA compliance, and zero-telemetry guarantee. |
| **[💡 Planned Features](Roadmap)** | Features, community requests, and ideas backlog under active investigation. |
| **[❓ Troubleshooting & FAQ](Troubleshooting-&-FAQ)** | Common error codes, SSL/TLS certificates, Modern Auth permissions, and solutions. |

---

## 🚀 Key Highlights & Architecture

### 1. Dual Interface & Cross-Platform Desktop Support
* **Windows Desktop (WPF):** Modern dark-mode interface built with Windows Presentation Foundation. Single-file standalone executable (`MundofyMailMigrator.exe`) with zero dependencies.
* **macOS Desktop (Avalonia UI 12):** Universal macOS Disk Image (`.dmg`) and application bundle (`.app.zip`) natively compiled for both Apple Silicon (M1/M2/M3/M4) and Intel (x64) Macs.
* **Headless CLI:** Can be executed via PowerShell, Command Prompt, or automated scripts using `-c <config_path>` without launching a window.

### 2. High-Speed Protocols & Native Encryption
* **Source:** IMAP (port 993 SSL/TLS or 143 STARTTLS), POP3 (port 995 SSL/TLS or 110 STARTTLS), Microsoft 365 (Modern OAuth2), and Google Workspace (Modern OAuth2).
* **Destination:** IMAP (port 993 SSL/TLS or 143 STARTTLS), Microsoft 365, and Google Workspace.
* **TLS 1.3 / SSL Support:** Fully integrated encryption without requiring third-party proxies (like stunnel).

### 3. Non-Destructive Deduplication
* Checks the unique RFC 822 Message-ID header of each email against destination mailboxes before transferring.
* If a migration is paused, stopped, or restarted, existing emails are automatically skipped—saving bandwidth and preventing duplicated emails.

### 4. Zero Installation Footprint
* Built on .NET 8 as self-contained executables.
* Requires no installer, no admin rights, and no pre-installed runtime dependencies.

---

## ⚡ Quick Start

1. **Download:** Grab the latest release from the [Releases Page](https://github.com/MundofyOfficial/Mundofy-MailMigrator/releases/latest):
   * **Windows:** `MundofyMailMigrator.exe`
   * **macOS:** `MundofyMailMigrator-macOS.dmg`
2. **Launch:**
   * On Windows: Double-click `MundofyMailMigrator.exe`. (If SmartScreen appears, click **More info** ➔ **Run anyway**).
   * On macOS: Mount the `.dmg`, drag to **Applications**, and open. (See the [macOS User Guide](macOS-User-Guide) for first-launch Gatekeeper instructions).
3. **Single Account:** Go to **Single Account Migration**, enter your source and destination credentials (or click **🔑 Sign In** for M365/Google), test the connections, and click **▶ Start Migration**.
4. **Batch Mode:** Go to **Batch Account Migration**, paste or import your account list, adjust concurrent worker threads, and click **▶ Start Batch Migration**.
