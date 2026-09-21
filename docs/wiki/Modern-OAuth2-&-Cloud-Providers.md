# Modern OAuth2 & Cloud Providers Guide

Modern email services—most notably **Microsoft 365 (Office 365 / Outlook)** and **Google Workspace (Gmail)**—have permanently deprecated legacy Basic Authentication (username + password) in favor of modern **OAuth 2.0 (SASL XOAUTH2)**.

**Mundofy MailMigrator** provides native, end-to-end OAuth2 support out of the box for both individual mailbox migrations and enterprise-wide batch migrations without requiring user passwords.

---

## 🔑 Single Account Migration: 1-Click Interactive Sign-In

For migrating individual mailboxes, Mundofy MailMigrator features an integrated browser loopback flow with PKCE (Proof Key for Code Exchange).

### 1. Microsoft 365 (Office 365 / Outlook)
1. In **Single Account Migration**, select **Microsoft 365** as the Source or Destination protocol.
2. Server settings are automatically locked and configured to `outlook.office365.com:993` with SSL enabled.
3. Click the **🔑 Sign In** button.
4. Your default web browser will open to Microsoft's secure authentication portal.
5. Sign in with your Microsoft 365 account credentials (supporting MFA, Authenticator, or FIDO2 security keys) and consent to mailbox access permissions.
6. Once authenticated, the browser displays a confirmation message, and MailMigrator automatically receives the OAuth2 token.
7. The button updates to a green badge displaying: `Connected: user@domain.com`.
8. To switch accounts or revoke access, click the **❌** clear button next to the badge.

### 2. Google Workspace & Gmail
1. In **Single Account Migration**, select **Google Workspace** as the Source or Destination protocol.
2. Server settings are automatically locked and configured to `imap.gmail.com:993` with SSL enabled.
3. Click the **🔑 Sign In** button.
4. Your web browser opens to Google's sign-in screen.
5. Sign in to your Google Workspace or Gmail account and grant access to read/write mailbox messages.
6. MailMigrator captures the SASL XOAUTH2 token via local loopback.
7. The status badge turns green (`Connected: user@domain.com`).

> [!NOTE]
> **No App Passwords Needed:** With modern OAuth2, you no longer need to enable legacy App Passwords or reduce your organization's security posture. (App passwords remain supported as a fallback when using standard IMAP).

---

## 🏢 Enterprise Batch Migrations (Zero User Passwords)

When migrating dozens or hundreds of corporate mailboxes, collecting individual user passwords is secure-risk and inefficient. Mundofy MailMigrator provides automated, tenant-wide batch authentication.

---

### Method A: Microsoft 365 Enterprise Batch (Azure App Registration)

Using Microsoft Entra ID (Azure AD), domain administrators can authorize MailMigrator at the tenant level. The migration engine dynamically acquires scoped access tokens for every target mailbox.

#### 1. Azure Portal Setup
1. Log in to the [Microsoft Entra Admin Center](https://entra.microsoft.com/) or Azure Portal.
2. Navigate to **Identity ➔ Applications ➔ App registrations ➔ New registration**.
3. Name the application (e.g., `Mundofy-MailMigrator-Batch`).
4. Select **Accounts in this organizational directory only (Single tenant)**.
5. Leave the Redirect URI blank and click **Register**.
6. Note down the **Application (client) ID** and **Directory (tenant) ID** from the Overview tab.

#### 2. Configure IMAP Permissions
1. Under your registered app, go to **API permissions ➔ Add a permission**.
2. Select **APIs my organization uses**, search for `Office 365 Exchange Online`, and select it.
3. Choose **Application permissions** (not Delegated permissions).
4. Check **`IMAP.AccessAsApp`** and click **Add permissions**.
5. Click **Grant admin consent for [Your Organization]** to approve the permission.

#### 3. Create Client Secret
1. Go to **Certificates & secrets ➔ Client secrets ➔ New client secret**.
2. Provide a description (e.g., `MailMigratorSecret`), set an expiration, and click **Add**.
3. Copy the **Value** immediately (this value will not be shown again).

#### 4. Configure MailMigrator Batch Tab
1. In **Batch Account Migration**, select **Microsoft 365** as the Source or Destination protocol.
2. Enter your **Tenant ID**, **Application (Client) ID**, and **Client Secret**.
3. Import or paste your mailbox list (only email addresses are needed; passwords are not required!).
4. The batch orchestrator dynamically generates per-user MSAL bearer tokens during migration.

---

### Method B: Google Workspace Enterprise Batch (Service Account JSON)

Google Workspace allows administrators to configure a Google Cloud Service Account with **Domain-Wide Delegation** to impersonate users across the entire domain without needing individual passwords.

#### 1. Google Cloud Console Setup
1. Navigate to the [Google Cloud Console](https://console.cloud.google.com/).
2. Create a new project (e.g., `Mundofy-Mail-Migration`).
3. Go to **APIs & Services ➔ Library**, search for **Gmail API**, and click **Enable**.
4. Go to **IAM & Admin ➔ Service Accounts ➔ Create Service Account**.
5. Provide a name (e.g., `mailmigrator-batch`) and click **Create and Continue**, then **Done**.
6. Click on the newly created service account, go to the **Keys** tab, click **Add Key ➔ Create new key**, choose **JSON**, and download the key file to your computer.
7. Open the **Details** tab of the service account and copy the **OAuth 2 Client ID** (a long numeric string).

#### 2. Authorize Domain-Wide Delegation in Google Workspace Admin
1. Open the [Google Workspace Admin Console](https://admin.google.com/) as a Super Admin.
2. Go to **Security ➔ Access and data control ➔ API controls**.
3. Under **Domain-wide delegation**, click **Manage Domain-Wide Delegation**.
4. Click **Add new**.
5. In the **Client ID** field, paste the numeric Client ID from step 1.7.
6. In **OAuth Scopes (comma-delimited)**, enter:
   ```text
   https://mail.google.com/
   ```
7. Click **Authorize**.

#### 3. Configure MailMigrator Batch Tab
1. In **Batch Account Migration**, select **Google Workspace** as the protocol.
2. Click **Browse** under Service Account Key and select your downloaded `.json` key file.
3. MailMigrator parses the credentials and verifies the client identity.
4. Import your account list (only email addresses are required).
5. During the migration, MailMigrator signs JWT assertions and impersonates each target mailbox subject automatically.

---

## 📋 Batch Table Dynamic Adaptation (v1.5.2)

When Microsoft 365 or Google Workspace is selected as the Source or Destination protocol:
* The username column headers dynamically adapt to **Source Mailbox (Email)** or **Dest Mailbox (Email)**.
* The password column headers adapt to **Source Auth** or **Dest Auth**, displaying an administrative badge: `🔑 Admin OAuth2`.
* Cell text editing for passwords is automatically locked, preventing operator confusion.
* You can import 1-column or 2-column CSVs containing only email addresses without providing placeholder passwords.
