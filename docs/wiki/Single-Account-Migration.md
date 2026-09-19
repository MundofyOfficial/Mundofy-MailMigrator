# Single Account Migration Guide

The **Single Account Migration** tab is designed for quick, ad-hoc migrations of individual mailboxes without needing to edit configuration files or spreadsheets.

---

## 📋 Step-by-Step Instructions

### Step 1: Configure the Source Server
1. **Username / Email:** Enter the full email address or login name.
   * ⚡ **Zero-Config Auto-Discovery:** As soon as you enter a valid email address or click the **⚡ Auto-Detect** button, Mundofy MailMigrator automatically queries provider registries, DNS MX records, and autoconfig endpoints to fill in the Server Host, Port, and SSL settings automatically!
2. **Protocol:** Select either:
   * **IMAP** (Default): Migrates all folders (Inbox, Sent, Drafts, Archives, Custom Folders).
   * **POP3**: Migrates only the primary Inbox (POP3 protocol does not support remote subfolders).
3. **Server:** Enter the hostname or IP address of the source email server (auto-detected, or e.g. mail.oldserver.com / imap.gmail.com).
4. **Port:** Standard ports (auto-filled):
   * 993 for IMAP with SSL/TLS.
   * 143 for IMAP with STARTTLS / Plain.
   * 995 for POP3 with SSL/TLS.
   * 110 for POP3 with Plain / STARTTLS.
5. **SSL / TLS Checkbox:** Keep checked for modern encrypted connections (recommended).
6. **Password:** Account password or App-Specific Password (required for Gmail and Microsoft 365).

### Step 2: Configure the Destination Server
1. **Username / Email:** The full email address on the new server.
   * ⚡ Click **⚡ Auto-Detect** to instantly discover target server host and port settings.
2. **Server:** Hostname of the target IMAP server (e.g. mail.newserver.com).
3. **Port:** Typically 993 (SSL/TLS) or 143.
4. **SSL / TLS Checkbox:** Kept checked for end-to-end encryption.
5. **Password:** Destination account password.

---

## 🔍 Connection Verification

Before running a migration, always verify connectivity by clicking:
* **🔍 Test Source Connection**
* **🔍 Test Dest Connection**

A prompt will confirm successful authentication or display exact error codes if authentication or firewall issues occur (e.g. invalid password, connection timeout, untrusted certificate).

---

## ▶ Running the Migration

1. Click **▶ Start Migration**.
2. **Live Monitoring:**
   * **Current Folder:** Displays the active folder being traversed (e.g., INBOX, Sent Items, Archives/2024).
   * **Speed Indicator:** Displays real-time throughput in **MB/s**.
   * **Progress Bar:** Reflects overall completion percentage based on total message counts.
   * **Metrics Cards:** Distinctly reports **New Copied** messages versus **Already on Dest** messages.
3. **Stopping:** You can click **⏹ Stop** at any time to safely halt processing.

---

## 💡 Pro-Tips
* **POP3 Source Consideration:** When selecting POP3 as the source, only the Inbox is migrated because the POP3 protocol cannot expose folder hierarchies. To migrate subfolders, use IMAP for both source and destination whenever possible.
* **SmartScreen / Network:** Ensure the Windows machine has unrestricted outbound access to ports 993/995.
