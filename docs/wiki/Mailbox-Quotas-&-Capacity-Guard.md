# Mailbox Quotas & Pre-Flight Capacity Guard

A common failure mode during email migrations occurs when a destination mailbox runs out of allocated disk space mid-transfer. When this happens, destination mail servers reject incoming messages with `NO [OVERQUOTA]` errors, resulting in incomplete migrations and manual remediation.

**Mundofy MailMigrator** introduces real-time **Mailbox Quota Checking** and an automated **Pre-Flight Destination Capacity Guard** to verify storage limits before a single byte is transferred.

---

## 📊 Protocol Implementation

### 1. RFC 2087 IMAP QUOTA Extension
When connecting to IMAP servers that support the RFC 2087 `QUOTA` capability (e.g. Dovecot, Zimbra, cPanel, Postfix/Courier, Exchange, Gmail):
* MailMigrator queries the server for root and folder quota roots.
* Storage metrics reported in standard 1024-octet blocks are parsed into exact byte counts, gigabytes (GB), and megabytes (MB).
* If an email provider provides unmetered or unlimited storage, MailMigrator cleanly detects the unconstrained quota root and indicates **Unlimited** rather than throwing a parsing error.

### 2. POP3 Storage Sizing
Because the legacy POP3 protocol does not provide an explicit quota query command:
* MailMigrator enumerates message octet lengths using the POP3 `LIST` command.
* Total mailbox size and message counts are aggregated in real-time to compute accurate source mailbox capacity.

---

## 🔍 Single Account Quota Monitoring

On the **Single Account Migration** tab:
1. When you click **🔍 Test Source Connection** or **🔍 Test Dest Connection**, the engine automatically interrogates the server's quota capabilities.
2. Direct storage indicators display underneath each server panel:
   * **Used Storage:** Current mailbox consumption (e.g. `8.42 GB`).
   * **Total Capacity:** Maximum allowed quota limit (e.g. `10.00 GB`).
   * **Utilization Percentage:** Dynamic bar displaying percentage consumed (e.g. `84%`).
3. If the server does not enforce storage quotas, a clean **Unlimited** indicator is shown.

---

## 👥 Batch Migration: Parallel Quota Check

For batch migrations containing dozens or hundreds of accounts, you can inspect storage utilization across all mailboxes concurrently.

### How to Check Batch Quotas:
1. Populate your accounts table in the **Batch Migration** tab.
2. Click the **📊 Check Quotas** button in the batch toolbar (or right-click the table and select **Check Mailbox Quotas**).
3. The orchestrator queries both source and destination mail servers in parallel according to your configured concurrency level.
4. Each row displays real-time color-coded health badges in the Quota column:

| Badge | Condition | Description |
| :--- | :--- | :--- |
| 🟢 **Normal** | `< 80%` | Sufficient storage available. Safe to proceed. |
| 🟡 **Warning** | `80% – 94%` | Approaching capacity. Monitor during transfer. |
| 🟠 **Critical** | `95% – 99%` | Severely constrained. High risk of reaching capacity during delta syncs. |
| 🔴 **Exceeded** | `≥ 100%` | Mailbox is already full. New messages will be rejected by destination server. |
| 🔵 **Unlimited** | Unmetered | Server does not enforce mailbox quotas. |

---

## 🛡️ Pre-Flight Destination Capacity Guard

Before any single or batch migration begins, MailMigrator's **Capacity Guard** runs an automated safety check:

1. **Comparison:** The guard calculates the source mailbox size and compares it against the remaining available free storage on the destination mailbox:
   $$\text{Free Storage}_{\text{Dest}} = \text{Quota Limit}_{\text{Dest}} - \text{Used Storage}_{\text{Dest}}$$
2. **Capacity Validation:** If $\text{Source Size} > \text{Free Storage}_{\text{Dest}}$, the migration is paused, and a detailed alert modal appears:
   > ⚠️ **Destination Mailbox Capacity Warning**  
   > *Source Mailbox Size:* **12.4 GB**  
   > *Destination Available Space:* **8.1 GB**  
   > *Storage Shortfall:* **-4.3 GB**  
   > *Migrating this account will likely trigger destination `[OVERQUOTA]` rejections.*
3. **Safety Controls:** The operator can choose to:
   * **Abort / Cancel:** Increase the mailbox quota on the destination server (or upgrade the user's hosting plan) before retrying.
   * **Proceed Anyway:** Bypass the alert if you plan to clean up destination storage concurrently.
