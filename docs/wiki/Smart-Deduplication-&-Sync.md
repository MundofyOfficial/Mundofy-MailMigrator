# Smart Deduplication & Incremental Sync

One of the most critical challenges during email migrations is ensuring that no duplicate emails are created and that migrations can be safely stopped, resumed, or re-run without re-copying existing messages.

---

## 🧠 How the Deduplication Engine Works

1. **RFC 822 `Message-ID` Hashing:**
   Every standard email contains a globally unique `Message-ID` header (e.g. `<CAFdE=...1234@mail.gmail.com>`).
2. **Pre-Traversal Indexing:**
   When migrating a folder (such as `INBOX`), Mundofy MailMigrator queries the destination folder to fetch all existing `Message-ID` hashes without downloading full message bodies.
3. **Smart Comparison:**
   As source emails are enumerated:
   * **If `Message-ID` exists on destination:** The message is recognized as already present and **skipped**.
   * **If `Message-ID` is missing:** The complete email message (headers, body, attachments, flags) is transferred securely and appended to the destination folder with preserved original dates and read/unread flags.
4. **Transparent Counter Metrics:**
   The UI and CLI report exact metrics:
   ```text
   Completed in 00:03! (0 new copied, 1,040 already on destination, 0 errors)
   ```

---

## 🔄 Cutover & Delta Re-Sync Strategy

For organizational migrations, administrators frequently perform an initial sync days before the official DNS MX cutover:

1. **Phase 1 (Pre-Sync):** Run Mundofy MailMigrator days prior to migration weekend. Copies 99% of historical emails.
2. **Phase 2 (Cutover Weekend):** Switch DNS MX records to the new email provider. New emails begin arriving at the new destination.
3. **Phase 3 (Delta Catch-up):** Run Mundofy MailMigrator again (simply click **▶ Start Batch Migration** to cleanly re-queue all accounts). Because of smart deduplication, the engine skips all pre-synced emails and transfers only the new messages received during the cutover window in seconds.
