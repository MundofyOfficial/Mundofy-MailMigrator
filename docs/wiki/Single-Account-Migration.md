# Single Account Migration Guide

The **Single Account Migration** tab is designed for quick migrations of individual mailboxes between IMAP, POP3, Microsoft 365, and Google Workspace without needing to edit configuration files or spreadsheets.

---

## 📋 Step-by-Step Instructions

### Step 1: Configure the Source Server
1. **Protocol:** Select your source provider or protocol:
   * **IMAP (Default):** Standard IMAP server migrating all folders (Inbox, Sent, Drafts, Archives, Custom Folders).
   * **POP3:** Legacy POP3 server migrating only the primary Inbox (POP3 protocol does not support remote subfolders).
   * **Microsoft 365:** Modern OAuth2 authentication for Office 365, Exchange Online, or Outlook.com mailboxes.
   * **Google Workspace:** Modern OAuth2 authentication for Google Workspace or personal Gmail accounts.
2. **Username / Email:** Enter the full email address.
   * ⚡ **Zero-Config Auto-Discovery:** When you enter an email address or click **⚡ Auto-Detect**, MailMigrator automatically queries DNS SRV records, MX records, provider registries, and autoconfig endpoints to resolve the host, port, and SSL encryption settings automatically!
3. **Authentication:**
   * **If using Microsoft 365 or Google Workspace:** Click **🔑 Sign In**. Your web browser opens to the provider's official sign-in portal. Sign in, complete MFA, and authorize access. MailMigrator captures the SASL XOAUTH2 token via loopback and displays a green `Connected: user@domain.com` badge.
   * **If using standard IMAP / POP3:** Enter your account password. (For older accounts requiring 2FA without OAuth2, use an App Password).
4. **Server & Port:** Standard secure endpoints:
   * IMAP SSL: Port `993`
   * IMAP Plain / STARTTLS: Port `143`
   * POP3 SSL: Port `995`
   * POP3 Plain / STARTTLS: Port `110`

### Step 2: Configure the Destination Server
1. **Protocol:** Choose **IMAP**, **Microsoft 365**, or **Google Workspace**.
2. **Email & Server Settings:** Enter destination email address and click **⚡ Auto-Detect** or configure manually.
3. **Authentication:** Click **🔑 Sign In** if migrating into Microsoft 365 or Google Workspace, or enter the destination password for standard IMAP.

---

## 🔍 Connection Verification & Quota Inspection

Before running a migration, verify credentials and review mailbox capacity by clicking:
* **🔍 Test Source Connection**
* **🔍 Test Dest Connection**

In addition to verifying logins, the engine automatically interrogates mailbox storage limits:
* **Used Storage & Total Quota:** Displays active disk usage vs. allocated limit (e.g., `4.2 GB / 10.0 GB (42%)`).
* **Health Indication:** Displays clean status badges for normal, warning, critical, or unmetered (unlimited) storage.

---

## ▶ Running the Migration

1. Click **▶ Start Migration**.
2. **Pre-Flight Capacity Check:** If the destination mailbox has less available free space than the total source mailbox size, the **Pre-Flight Capacity Guard** displays an alert modal warning of potential `[OVERQUOTA]` failures, allowing you to resize the quota before proceeding.
3. **Live Monitoring:**
   * **Current Folder:** Displays the active folder being traversed (e.g., `INBOX`, `Sent Items`, `Archives/2024`).
   * **Speed Indicator:** Displays real-time throughput in **MB/s**.
   * **Progress Bar:** Reflects overall completion percentage based on total message counts.
   * **Metrics Cards:** Distinctly reports **New Copied** messages versus **Already on Dest** messages.
4. **Safe Halt:** Click **⏹ Stop** at any time to safely halt processing. Transferred messages remain intact and will be skipped if restarted.

---

## 💡 Pro-Tips

* **Modern OAuth2 vs. App Passwords:** For Microsoft 365 and Google Workspace, always prefer the **🔑 Sign In** button over generating legacy app passwords. It uses secure, short-lived tokens and requires no tenant policy downgrades.
* **POP3 Source Consideration:** When selecting POP3 as the source, only the Inbox is migrated because the POP3 protocol cannot expose folder hierarchies. To migrate subfolders, use IMAP for both source and destination whenever possible.
* **Automatic Sleep Prevention:** MailMigrator automatically keeps your computer awake during active migrations while allowing screens to power off.
