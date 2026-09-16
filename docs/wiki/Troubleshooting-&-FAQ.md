# Troubleshooting & FAQ

Common questions, error resolutions, and security configurations.

---

## ❓ Frequently Asked Questions

### Q: Does the tool run completely offline and locally?
**A:** Yes. Mundofy MailMigrator runs entirely on your local Windows machine. All connections occur directly between your PC, the source server, and the destination server. Zero email content, credentials, or telemetry are transmitted to Mundofy or any third party.

### Q: Why do I see a "Windows protected your PC" (SmartScreen) warning?
**A:** Because Mundofy MailMigrator is an open-source tool distributed without a commercial EV code signing certificate. To run:
1. Click **"More info"**.
2. Click **"Run anyway"**.

---

## 🛠️ Common Errors & Fixes

### 1. Authentication Failed (`NO [AUTHENTICATIONFAILED]`)
* **Gmail:** Normal account passwords will be rejected. You must enable 2-Step Verification in Google Account Settings and generate an **App Password** (16-character code).
* **Microsoft 365 / Outlook:** Ensure IMAP is enabled in the Exchange Admin Center / Microsoft 365 Admin Portal under *Users ➔ Active Users ➔ Mail ➔ Manage email apps ➔ IMAP*.
* **Username Format:** Some servers require `user@domain.com` while others require only `user`.

### 2. SSL/TLS Certificate Errors (`UntrustedRoot` / `CertificateValidation`)
* If migrating from an internal server with a self-signed SSL certificate, verify the certificate is installed in the Windows Trusted Root Certification Authorities store.

### 3. Connection Timed Out
* Verify that the Windows Firewall or corporate router is not blocking outbound traffic to port `993` (IMAP SSL) or `995` (POP3 SSL).
* If your email provider uses non-standard ports (e.g., cPanel custom SSL ports), override the port in the Port field.

### 4. Folder Deselection / IMAP Concurrency Limits
* Some mail hosts (e.g. Hostinger, cPanel, Dovecot) enforce limits on concurrent connections per IP address (e.g. maximum 10 simultaneous connections). If you encounter connection drops, reduce the **Concurrent Workers** slider to `2` or `4`.
