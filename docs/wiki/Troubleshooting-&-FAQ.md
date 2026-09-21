# Troubleshooting & FAQ

Common questions, authentication error resolutions, and security configurations.

---

## ❓ Frequently Asked Questions

### Q: Does the tool run completely offline and locally?
**A:** Yes. Mundofy MailMigrator runs 100% locally on your machine. Encrypted network connections are established directly between your computer and your email servers. Zero email content, account credentials, or telemetry data are ever transmitted to Mundofy or third parties.

### Q: Why do I see a "Windows protected your PC" (SmartScreen) warning?
**A:** Because Mundofy MailMigrator is an open-source tool distributed without an expensive commercial Microsoft EV code signing certificate. To run:
1. Click **More info**.
2. Click **Run anyway**.

### Q: Why do I see a security prompt on macOS ("cannot be opened")?
**A:** On macOS, Gatekeeper prompts on first launch for apps downloaded outside the Mac App Store:
1. Open **System Settings ➔ Privacy & Security**.
2. Scroll to Security and click **Open Anyway** next to Mundofy MailMigrator.  
*(Alternatively, run `xattr -d com.apple.quarantine /Applications/MundofyMailMigrator.app` in Terminal).* See the [macOS User Guide](macOS-User-Guide) for details.

### Q: Does MailMigrator support Google Workspace and Microsoft 365?
**A:** Yes! Version 1.5.0+ natively supports modern OAuth2:
* **Single Accounts:** Click the **🔑 Sign In** button to sign in directly through your browser.
* **Enterprise Batching:** Use Azure App Registration (`IMAP.AccessAsApp`) or Google Service Account JSON keys with Domain-Wide Delegation to migrate entire organizations without needing user passwords. See the [Modern OAuth2 Guide](Modern-OAuth2-&-Cloud-Providers).

---

## 🛠️ Common Errors & Solutions

### 1. Authentication Failed (`NO [AUTHENTICATIONFAILED]`)
* **Google Workspace / Gmail:**
  * *Recommended:* Use the **🔑 Sign In** button for seamless OAuth2.
  * *Fallback:* If using standard IMAP, normal Google passwords will be rejected. You must enable 2-Step Verification and generate a 16-character **App Password** in Google Account Security.
  * *Enterprise Batch:* Verify in Google Workspace Admin Console that your Service Account Client ID is authorized under **Domain-Wide Delegation** with the scope `https://mail.google.com/`.
* **Microsoft 365 / Exchange Online:**
  * *Recommended:* Use the **🔑 Sign In** button for seamless OAuth2.
  * *Fallback:* If using IMAP basic auth, ensure IMAP protocol access is enabled for the mailbox in Microsoft 365 Admin Portal (*Users ➔ Active Users ➔ Mail ➔ Manage email apps ➔ IMAP*).
  * *Enterprise Batch:* Ensure the Azure App Registration has the **Application permission** `Office 365 Exchange Online ➔ IMAP.AccessAsApp` (not Delegated) and that an administrator clicked **Grant admin consent**.
* **Username Format:** Some mail servers require the full email (`user@domain.com`) while others accept only the username (`user`).

### 2. Destination Storage Exceeded (`NO [OVERQUOTA]`)
* **Cause:** The destination mailbox has exceeded its allocated disk quota or does not have enough free space to receive incoming messages.
* **Prevention:** Run **📊 Check Quotas** before migrating. MailMigrator's **Pre-Flight Capacity Guard** automatically warns you if source data exceeds destination free space.
* **Solution:** Increase the mailbox storage limit in your hosting control panel (cPanel, Plesk, DirectAdmin, or Exchange Admin Center) before resuming transfer.

### 3. SSL/TLS Certificate Errors (`UntrustedRoot` / `CertificateValidation`)
* **GUI Bypass:** In the Desktop GUI, open **Settings & Logs** and check **"Permit Self-Signed / Invalid SSL Certificates"**.
* **CLI Bypass:** Pass the `-k` or `--allow-invalid-certs` (or `--insecure`) flag when launching the CLI:
  ```powershell
  .\MundofyMailMigrator.exe -c ImapCopy.cfg -k
  ```
* **Configuration Directive:** In your `ImapCopy.cfg` file, add:
  ```ini
  AllowInvalidCertificates Yes
  ```
* **System Store:** Alternatively, install the server's root CA certificate into the operating system's Trusted Root Certification Authorities store.

### 4. Connection Timed Out
* Verify that your local firewall or corporate router allows outbound TCP traffic on port `993` (IMAP SSL) or `995` (POP3 SSL).
* If your email host uses non-standard ports (e.g., custom cPanel SSL ports), override the default in the Port field.

### 5. Server Throttling & Connection Drops
* Some mail providers (e.g., Hostinger, Dovecot, Office 365) enforce connection or rate limits per IP address (e.g. max 10 concurrent connections). If you encounter intermittent drops or connection rejections, reduce the **Concurrent Workers** slider to `2` or `4`.
