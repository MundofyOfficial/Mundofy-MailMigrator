# Welcome to the Mundofy MailMigrator Wiki

**Mundofy MailMigrator** is a modern, high-speed email migration suite engineered for Windows, supporting both an interactive desktop GUI and a headless command-line interface (CLI). It enables fast, secure, and lossless mailbox migrations between IMAP and POP3 email servers with native TLS 1.3 encryption, concurrent multi-threading, and intelligent RFC 822 Message-ID deduplication.

---

## 💡 Why This Tool Was Created

As a managed hosting provider, we routinely help customers migrate their mailboxes over to our servers. Migrating email can be technically intimidating, and most users simply don't have the specialized knowledge or command-line experience to do it themselves safely without risking lost messages or downtime.

For years, the go-to tool for this job was the classic open-source **`imapcopy`** by Armin Diehl. It was simple, reliable, and sysadmins everywhere swore by it.

Over time, however, email infrastructure evolved. Mail servers began dropping older protocols and strictly enforcing **TLS 1.3**. Because classic `imapcopy` hasn't been maintained in years, connection handshakes started failing. Keeping it running required clumsy workarounds like running local `stunnel` proxy daemons, and it still lacked support for POP3, multi-threading, or modern error handling.

Patching an outdated utility was no longer the smartest way forward. So with an open mind, we asked a simple question:

> *"What if we create a modern tool that does everything imapcopy did, but so much more?"*

That idea became **Mundofy MailMigrator**:
* **Native TLS 1.3 & SSL out-of-the-box** — zero proxies or wrappers needed.
* **⚡ Zero-Config Server Auto-Discovery** — automatically resolves IMAP host, port, and SSL from any email address.
* **100% backward compatibility** with classic `ImapCopy.cfg` configuration files.
* **POP3 source support** for migrating older mailboxes that don't have IMAP enabled.
* **Concurrent multi-account batching** with an intuitive Desktop GUI alongside the CLI.
* **Smart RFC 822 Message-ID deduplication** for lossless, resume-friendly transfers.

Rather than keeping this as an internal utility, we open-sourced it for everyone. Whether you're a sysadmin managing domain migrations or someone moving an individual inbox, this tool was built to make email migration painless—and with the help of the open-source community, our goal is to make this the modern, free go-to tool for everyone.

---

## 📖 Wiki Table of Contents

| Section | Description |
| :--- | :--- |
| **[👤 Single Account Migration](Single-Account-Migration)** | Step-by-step guide to migrating an individual mailbox between IMAP/POP3 servers. |
| **[👥 Batch Migration & Concurrency](Batch-Migration-&-Multi-Threading)** | Managing enterprise migrations with parallel workers, spreadsheet imports, and DataGrid editing. |
| **[💻 CLI & Headless Automation](CLI-&-Headless-Automation)** | Scripting migrations in CI/CD, scheduled tasks, and headless Windows Server environments. |
| **[📄 Configuration File Format](Configuration-File-Format)** | Deep dive into legacy ImapCopy.cfg syntax, directives, and CSV/TSV table formats. |
| **[🔄 Smart Deduplication & Sync](Smart-Deduplication-&-Sync)** | Understanding how zero-duplicate transfers and incremental resumption work. |
| **[💡 Planned Features](Roadmap)** | Features, community requests, and ideas backlog under investigation. |
| **[❓ Troubleshooting & FAQ](Troubleshooting-&-FAQ)** | Common error codes, SSL/TLS certificates, App Passwords (Gmail/M365), and solutions. |

---

## 🚀 Key Highlights & Architecture

### 1. Dual Interface Mode
* **Desktop GUI (WPF):** Modern dark-mode interface built with Windows Presentation Foundation. Includes interactive form fields, connection testers, a live DataGrid batch table, and real-time migration metrics (transfer speed in MB/s, progress bar, folder traversal).
* **Headless CLI:** Can be executed via PowerShell, Command Prompt, or automated scripts using -c <config_path> without launching a window.

### 2. High-Speed Protocols & Native Encryption
* **Source:** IMAP (port 993 SSL/TLS or 143 STARTTLS) and POP3 (port 995 SSL/TLS or 110 STARTTLS).
* **Destination:** IMAP (port 993 SSL/TLS or 143 STARTTLS).
* **TLS 1.3 / SSL Support:** Fully integrated encryption without requiring third-party proxies (like stunnel).

### 3. Non-Destructive Deduplication
* Checks the unique RFC 822 Message-ID header of each email against destination mailboxes before transferring.
* If a migration is paused, stopped, or restarted, existing emails are automatically skipped—saving bandwidth and preventing duplicated emails.

### 4. Zero Installation Footprint
* Built on .NET 8 as a self-contained, single-file Windows executable.
* Requires no installer, no admin rights, and no pre-installed .NET runtimes.

---

## ⚡ Quick Start

1. **Download:** Grab the latest MundofyMailMigrator.exe from the [Releases Page](https://github.com/MundofyOfficial/Mundofy-MailMigrator/releases/latest).
2. **Launch:** Double-click the file to open the Desktop GUI.
3. **Single Account:** Go to **Single Account Migration**, enter your source and destination credentials, test the connections, and click **▶ Start Migration**.
4. **Batch Mode:** Go to **Batch Account Migration**, paste or import your account list, adjust concurrent worker threads, and click **▶ Start Batch Migration**.
