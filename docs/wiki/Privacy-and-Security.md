# Privacy, Security & Zero-Telemetry Guarantee

Mundofy MailMigrator was built on an uncompromising commitment to privacy and data security. This guide details how the application handles your data, its standalone architecture, its dedicated in-app **Privacy & Compliance** tab, and its compliance with global data protection frameworks.

---

## 1. Zero-Telemetry & Zero-Tracking Architecture

Unlike cloud-based migration services that require you to upload your server credentials to an external dashboard or route emails through proprietary proxy servers, **Mundofy MailMigrator runs 100% locally on your computer**:

* **Direct Point-to-Point TLS Handshakes:** All network connections are opened directly between your local computer, your source email server, and your destination email server.
* **In-Memory Streaming:** Messages and folder structures stream directly in-memory from the source to destination. No email files, attachments, or passwords are saved to temporary cache files on your hard drive.
* **Zero Cloud Intermediaries:** Mundofy operates no intermediary servers, cloud relays, or message queues. Your data never touches any infrastructure owned or controlled by Mundofy.
* **Zero Analytics & Diagnostic Siphoning:** The application contains no telemetry trackers, no user behavior monitoring, and no automated crash dump uploads.

```text
┌─────────────────────────┐          TLS 1.3          ┌──────────────────────────────────┐          TLS 1.3          ┌──────────────────────────┐
│   Source Mail Server    │ ◄───────────────────────► │   Your Computer (MailMigrator)   │ ◄───────────────────────► │  Destination Mail Server │
│   (IMAP / POP3 / SSL)   │     Direct Connection     │  Pure In-Memory • Zero Telemetry │     Direct Connection     │        (IMAP / SSL)      │
└─────────────────────────┘                           └──────────────────────────────────┘                           └──────────────────────────┘
```

---

## 2. Dedicated In-App Privacy & Compliance Tab

Starting in v1.4.0, Mundofy MailMigrator features a dedicated, native **Privacy & Compliance** tab inside the desktop application. This tab provides complete transparency regarding:
* Architectural guarantees and local socket handling.
* Legal breakdown of GDPR, CCPA, and HIPAA compliance.
* Source code links to independently verify every line of networking code.

---

## 3. Worldwide Privacy Law Compliance

Because Mundofy MailMigrator collects, stores, transmits, and processes **zero personal data**:

### 🇪🇺 European Union GDPR (General Data Protection Regulation)
* **Privacy by Design and by Default (Articles 5 & 25):** The system architecture prevents any external data transmission.
* **No Data Processing Agreement (DPA) Needed:** Because Mundofy never receives, accesses, or processes your mail data, Mundofy is neither a **Data Controller** nor a **Data Processor** under GDPR definitions.

### 🇺🇸 California Consumer Privacy Act / CPRA (CCPA)
* No personal consumer information is ever collected, sold, rented, or shared with third parties or data brokers.
* No persistent device IDs or advertising identifiers are accessed.

### 🇨🇦 Canadian PIPEDA & UK Data Protection Act
* Fully conforms with international principles of data minimization and purpose limitation.

### 🏥 Healthcare & Financial Compliance (HIPAA & SOC 2 Friendly)
* Regulated organizations (hospitals, clinics, accounting firms, financial institutions) can perform email migrations without executing external Business Associate Agreements (BAAs) with Mundofy, because the migration runs entirely within the organization's existing administrative perimeter.

---

## 4. Complete Network Transparency: The One Single Ping

To ensure radical transparency, here is the **only external network request** Mundofy MailMigrator ever initiates outside of your specified email servers:

* **Endpoint:** `GET https://api.github.com/repos/MundofyOfficial/Mundofy-MailMigrator/releases/latest`
* **Purpose:** Queries the public GitHub Releases API to compare the latest published tag against your installed version string (e.g. `v1.6.0`), notifying you if an update is available with security fixes or improvements.
* **Data Transmitted:** An anonymous HTTP request with a standard User-Agent header (e.g., `Mundofy-MailMigrator/1.6.0`). **No IP logging, no machine GUIDs, no usernames, and zero telemetry data.**
* **Offline / Air-Gapped Environments:** In isolated internal networks without internet access, this check silently and harmlessly times out. The core migration engine continues to operate with 100% functionality.

---

## 5. Open-Source Independent Verifiability

Trust requires proof:
1. **Source Code Inspection:** Every line of code is open-source on [GitHub](https://github.com/MundofyOfficial/Mundofy-MailMigrator) under the [MIT License](https://github.com/MundofyOfficial/Mundofy-MailMigrator/blob/main/LICENSE).
2. **Network Packet Capture:** You can monitor network traffic in real-time with tools like Wireshark to verify that only encrypted sockets to your mail servers and the anonymous GitHub version check occur.
3. **Reproducible Builds:** You can clone the repository and compile the application directly from source using the official .NET 8 SDK.
