# macOS User Guide

**Mundofy MailMigrator** features a dedicated, native cross-platform application for **macOS**, engineered with Avalonia UI 12. It provides 100% feature parity with the Windows desktop application, including native TLS 1.3 socket streaming, modern OAuth2 authentication for Microsoft 365 and Google Workspace, concurrent batch processing, and live mailbox quota inspections.

---

## 💻 System Requirements & Architecture

* **Architecture:** Universal macOS binary supporting both **Apple Silicon** (M1, M2, M3, M4) and **Intel** (x64) Macs.
* **macOS Versions:** macOS 12 (Monterey), macOS 13 (Ventura), macOS 14 (Sonoma), and macOS 15 (Sequoia).
* **Dependencies:** None. Mundofy MailMigrator is compiled as a self-contained application with embedded runtimes. No .NET runtime or third-party frameworks need to be installed.

---

## 📥 Download & Installation

1. Go to the [Releases Page](https://github.com/MundofyOfficial/Mundofy-MailMigrator/releases/latest).
2. Download either:
   * **Universal Disk Image (Recommended):** `MundofyMailMigrator-macOS.dmg`
   * **Zipped Application Bundle:** `MundofyMailMigrator-macOS.app.zip`
3. **Mount and Install:**
   * Double-click `MundofyMailMigrator-macOS.dmg`.
   * Drag the **Mundofy MailMigrator** icon into your **Applications** folder.
   * Eject the disk image.

---

## 🛡️ macOS Gatekeeper & First-Time Launch

Because Mundofy MailMigrator is a community open-source project distributed outside the Mac App Store without an Apple Developer ID certificate, macOS Gatekeeper may present a security prompt on the first launch:

> *"Mundofy MailMigrator can't be opened because Apple cannot check it for malicious software."*  
> or  
> *"Mundofy MailMigrator is an app downloaded from the internet. Are you sure you want to open it?"*

### How to Permit Launch:

#### Method 1: System Settings (GUI)
1. Double-click the application in **Applications**. When the dialog appears, click **Cancel**.
2. Open **System Settings** (or *System Preferences*).
3. Navigate to **Privacy & Security**.
4. Scroll down to the **Security** section. You will see:  
   *"Mundofy MailMigrator was blocked from use because it is not from an identified developer."*
5. Click **Open Anyway** and enter your Mac administrator password or Touch ID.
6. Click **Open** on the final confirmation prompt.

#### Method 2: Terminal Command (Instant Bypass)
If you prefer the command line, open **Terminal** and remove the quarantine attribute:
```bash
xattr -d com.apple.quarantine /Applications/MundofyMailMigrator.app
```

Once permitted, macOS remembers your approval and the app will open immediately on all future launches.

---

## 🚀 Key Features on macOS

* **Universal High-Performance Engine:** Native ARM64 execution on Apple Silicon with hardware-accelerated TLS 1.3 cryptography.
* **Modern OAuth2 Sign-In:** 1-click browser sign-in for Microsoft 365 and Google Workspace accounts with secure loopback token handling.
* **Enterprise Batch Migrations:** Full support for Azure App Registration tenant migrations and Google Cloud Service Account JSON keys with Domain-Wide Delegation.
* **Mailbox Quota Inspector & Capacity Guard:** Query RFC 2087 IMAP and POP3 mailbox capacity metrics and prevent `[OVERQUOTA]` storage rejections before starting transfers.
* **Native In-App Update Badge:** The macOS title bar automatically displays an update indicator when a newer release is published on GitHub, allowing 1-click navigation to download updates.

---

## 💡 macOS Tips & Best Practices

* **Overnight Transfers & App Nap:** When running large, multi-gigabyte batch migrations, ensure your Mac's Energy Saver / Battery settings do not put the computer to sleep while connected to power.
* **Firewall / Network Access:** Ensure macOS Application Firewall allows outbound TCP connections on port `993` (IMAP SSL), `995` (POP3 SSL), and `143`/`110` (STARTTLS).
* **Config Compatibility:** Configuration files (`ImapCopy.cfg`) and CSV spreadsheets created on Windows or Linux can be loaded into the macOS application with 100% compatibility.
