using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Security;
using MimeKit;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Services;

public class ImapMigrationService
{
    public event EventHandler<LogEntry>? LogEmitted;

    public void Log(LogLevel level, string message)
    {
        LogEmitted?.Invoke(this, new LogEntry { Level = level, Message = message });
    }

    public async Task<(bool Success, string Message, MailboxQuotaInfo Quota)> TestConnectionWithQuotaAsync(
        ServerEndpoint endpoint,
        string username,
        string password,
        CancellationToken ct = default)
    {
        var quotaInfo = new MailboxQuotaInfo();

        try
        {
            var (host, port) = ResolveHostAndPort(endpoint);

            if (endpoint.Protocol == ServerProtocol.Pop3)
            {
                using var client = CreatePop3Client(endpoint);
                await client.ConnectAsync(host, port, GetSecureSocketOptions(endpoint), ct);
                await client.AuthenticateAsync(username, password, ct);

                int count = client.Count;
                quotaInfo.MessageCountUsed = count;

                try
                {
                    var sizes = await client.GetMessageSizesAsync(ct);
                    long totalBytes = 0;
                    foreach (var s in sizes) totalBytes += s;
                    quotaInfo.StorageUsedBytes = totalBytes;
                }
                catch { }

                await client.DisconnectAsync(true, ct);

                string sizeStr = quotaInfo.StorageUsedBytes.HasValue
                    ? $" ({count:N0} messages, {MailboxQuotaInfo.FormatBytes(quotaInfo.StorageUsedBytes.Value)})"
                    : $" ({count:N0} messages)";
                return (true, $"POP3 Connected successfully!{sizeStr}", quotaInfo);
            }
            else
            {
                using var client = CreateImapClient(endpoint);
                await client.ConnectAsync(host, port, GetSecureSocketOptions(endpoint), ct);
                await AuthenticateImapClientAsync(client, endpoint, username, password, ct);

                if (client.Capabilities.HasFlag(MailKit.Net.Imap.ImapCapabilities.Quota))
                {
                    try
                    {
                        var inbox = client.Inbox;
                        await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, ct);
                        var quota = await inbox.GetQuotaAsync(ct);
                        if (quota != null)
                        {
                            quotaInfo.IsSupported = true;
                            quotaInfo.QuotaRoot = quota.QuotaRoot?.FullName ?? quota.QuotaRoot?.Name ?? "";
                            // RFC 2087: IMAP QUOTA STORAGE resources are reported in units of 1024 octets (kilobytes)
                            if (quota.StorageLimit.HasValue) quotaInfo.StorageLimitBytes = (long)quota.StorageLimit.Value * 1024L;
                            if (quota.CurrentStorageSize.HasValue) quotaInfo.StorageUsedBytes = (long)quota.CurrentStorageSize.Value * 1024L;
                            if (quota.MessageLimit.HasValue) quotaInfo.MessageCountLimit = (long)quota.MessageLimit.Value;
                            if (quota.CurrentMessageCount.HasValue) quotaInfo.MessageCountUsed = (long)quota.CurrentMessageCount.Value;
                        }
                        quotaInfo.MessageCountUsed ??= inbox.Count;
                        if (!quotaInfo.StorageUsedBytes.HasValue && inbox.Count == 0)
                        {
                            quotaInfo.StorageUsedBytes = 0;
                        }
                        await inbox.CloseAsync(false, ct);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        var inbox = client.Inbox;
                        await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, ct);
                        quotaInfo.MessageCountUsed = inbox.Count;
                        await inbox.CloseAsync(false, ct);
                    }
                    catch { }
                }

                await client.DisconnectAsync(true, ct);

                string quotaSummary = quotaInfo.IsSupported && quotaInfo.StorageLimitBytes.HasValue
                    ? $" ({quotaInfo.FormattedSummary})"
                    : (quotaInfo.MessageCountUsed.HasValue ? $" ({quotaInfo.MessageCountUsed.Value:N0} messages in Inbox)" : "");

                return (true, $"IMAP Connected successfully!{quotaSummary}", quotaInfo);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, quotaInfo);
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        ServerEndpoint endpoint,
        string username,
        string password,
        CancellationToken ct = default)
    {
        var result = await TestConnectionWithQuotaAsync(endpoint, username, password, ct);
        return (result.Success, result.Message);
    }

    public async Task<MailboxQuotaInfo> GetMailboxQuotaAsync(
        ServerEndpoint endpoint,
        string username,
        string password,
        CancellationToken ct = default)
    {
        var result = await TestConnectionWithQuotaAsync(endpoint, username, password, ct);
        return result.Quota;
    }

    public async Task MigrateAccountAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        AccountJob job,
        MigrationOptions options,
        IProgress<AccountJob>? progress = null,
        CancellationToken ct = default)
    {
        job.Status = MigrationStatus.InProgress;
        job.StatusMessage = "Starting migration...";
        job.CopiedMessages = 0;
        job.SkippedMessages = 0;
        job.FailedMessages = 0;
        job.BytesTransferred = 0;
        progress?.Report(job);

        Log(LogLevel.Info, $"[{job.SourceUser}] Starting migration to destination [{job.DestUser}]");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (source.Protocol == ServerProtocol.Pop3)
            {
                await MigratePop3ToImapAsync(source, dest, job, options, stopwatch, progress, ct);
            }
            else
            {
                await MigrateImapToImapAsync(source, dest, job, options, stopwatch, progress, ct);
            }

            job.Status = MigrationStatus.Completed;
            job.StatusMessage = $"Completed in {stopwatch.Elapsed:mm\\:ss}! ({job.CopiedMessages} new copied, {job.SkippedMessages} already on destination, {job.FailedMessages} errors)";
            job.TransferSpeed = CalculateSpeed(job.BytesTransferred, stopwatch.Elapsed);
            Log(LogLevel.Success, $"[{job.SourceUser}] Finished: {job.CopiedMessages} new copied, {job.SkippedMessages} already found on destination, {job.FailedMessages} errors, {FormatBytes(job.BytesTransferred)} transferred.");
        }
        catch (OperationCanceledException)
        {
            job.Status = MigrationStatus.Paused;
            job.StatusMessage = "Paused/Cancelled";
            Log(LogLevel.Warning, $"[{job.SourceUser}] Migration was paused or cancelled.");
        }
        catch (Exception ex)
        {
            job.Status = MigrationStatus.Failed;
            job.StatusMessage = $"Error: {ex.Message}";
            Log(LogLevel.Error, $"[{job.SourceUser}] Migration failed: {ex.Message}");
        }
        finally
        {
            progress?.Report(job);
        }
    }

    private async Task MigrateImapToImapAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        AccountJob job,
        MigrationOptions options,
        Stopwatch stopwatch,
        IProgress<AccountJob>? progress,
        CancellationToken ct)
    {
        using var srcClient = CreateImapClient(source);
        using var dstClient = CreateImapClient(dest);

        job.StatusMessage = "Connecting to source IMAP server...";
        progress?.Report(job);
        var (srcHost, srcPort) = ResolveHostAndPort(source);
        await srcClient.ConnectAsync(srcHost, srcPort, GetSecureSocketOptions(source), ct);
        string srcToken = !string.IsNullOrWhiteSpace(job.SourceOAuthToken) ? job.SourceOAuthToken : job.SourcePassword;
        await AuthenticateImapClientAsync(srcClient, source, job.SourceUser, srcToken, ct);

        job.StatusMessage = "Connecting to destination IMAP server...";
        progress?.Report(job);
        var (dstHost, dstPort) = ResolveHostAndPort(dest);
        await dstClient.ConnectAsync(dstHost, dstPort, GetSecureSocketOptions(dest), ct);
        string dstToken = !string.IsNullOrWhiteSpace(job.DestOAuthToken) ? job.DestOAuthToken : job.DestPassword;
        await AuthenticateImapClientAsync(dstClient, dest, job.DestUser, dstToken, ct);

        // Enumerate all source folders
        job.StatusMessage = "Enumerating folders...";
        progress?.Report(job);

        var srcFolders = new List<IMailFolder>();
        if (srcClient.PersonalNamespaces.Count > 0)
        {
            foreach (var ns in srcClient.PersonalNamespaces)
            {
                var root = srcClient.GetFolder(ns);
                srcFolders.AddRange(await GetAllSubfoldersAsync(root, ct));
            }
        }
        else
        {
            var root = srcClient.GetFolder(srcClient.Inbox.FullName);
            srcFolders.AddRange(await GetAllSubfoldersAsync(root, ct));
        }

        if (!srcFolders.Any(f => f.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase) || f.FullName.Equals(srcClient.Inbox.FullName, StringComparison.OrdinalIgnoreCase)))
        {
            srcFolders.Insert(0, srcClient.Inbox);
        }

        // Count total messages across target folders using STATUS command (doesn't open/lock folders)
        int totalMsgs = 0;
        var foldersToProcess = new List<IMailFolder>();
        foreach (var folder in srcFolders)
        {
            if ((folder.Attributes & FolderAttributes.NoSelect) != 0)
                continue;

            if (!options.ShouldCopyFolder(folder.FullName))
            {
                Log(LogLevel.Info, $"[{job.SourceUser}] Skipping folder '{folder.FullName}' per filters");
                continue;
            }

            int count = 0;
            try
            {
                await folder.StatusAsync(StatusItems.Count, ct);
                count = folder.Count;
            }
            catch
            {
                try
                {
                    await folder.OpenAsync(FolderAccess.ReadOnly, ct);
                    count = folder.Count;
                    await folder.CloseAsync(false, ct);
                }
                catch
                {
                    count = 0;
                }
            }

            totalMsgs += count;
            foldersToProcess.Add(folder);
        }

        job.TotalMessages = totalMsgs;
        progress?.Report(job);

        // Process each folder one by one with proper Open/Close lifecycle
        foreach (var srcFolder in foldersToProcess)
        {
            if (ct.IsCancellationRequested) break;

            job.CurrentFolder = srcFolder.FullName;

            // Open source folder for reading
            await srcFolder.OpenAsync(FolderAccess.ReadOnly, ct);
            try
            {
                job.StatusMessage = $"Processing folder '{srcFolder.FullName}' ({srcFolder.Count} msgs)...";
                progress?.Report(job);

                if (srcFolder.Count == 0)
                {
                    if (options.CreateEmptyFolders)
                    {
                        // Ensure the empty folder exists on destination
                        await ResolveOrCreateDestinationFolderAsync(dstClient, srcFolder, options, ct);
                    }
                    continue;
                }

                // Map folder name to destination and open for writing
                IMailFolder dstFolder = await ResolveOrCreateDestinationFolderAsync(dstClient, srcFolder, options, ct);
                await dstFolder.OpenAsync(FolderAccess.ReadWrite, ct);
                try
                {
                    // Deduplication: retrieve existing Message-IDs and fallback fingerprints from destination folder
                    var existingMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var existingFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (options.Deduplicate && dstFolder.Count > 0)
                    {
                        job.StatusMessage = $"Checking for existing messages in '{dstFolder.FullName}'...";
                        progress?.Report(job);

                        var dstItems = MessageSummaryItems.Envelope |
                                       MessageSummaryItems.InternalDate |
                                       MessageSummaryItems.Size;
                        var dstSummaries = await dstFolder.FetchAsync(0, -1, dstItems, ct);

                        foreach (var s in dstSummaries)
                        {
                            var normId = NormalizeMessageId(s.Envelope?.MessageId);
                            if (normId != null)
                            {
                                existingMessageIds.Add(normId);
                            }

                            var fp = BuildFingerprint(s.Envelope?.Date ?? s.InternalDate, s.Envelope?.Subject, s.Envelope?.From?.ToString());
                            if (fp != null)
                            {
                                existingFingerprints.Add(fp);
                            }
                        }
                    }

                    // Fetch messages from source
                    var itemsToFetch = MessageSummaryItems.UniqueId |
                                       MessageSummaryItems.Flags |
                                       MessageSummaryItems.InternalDate |
                                       MessageSummaryItems.Size |
                                       MessageSummaryItems.Envelope;

                    var summaries = await srcFolder.FetchAsync(0, -1, itemsToFetch, ct);

                    for (int i = 0; i < summaries.Count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var summary = summaries[i];
                        var rawMsgId = summary.Envelope?.MessageId;
                        var normMsgId = NormalizeMessageId(rawMsgId);
                        var msgDate = summary.Envelope?.Date ?? summary.InternalDate;

                        // Date range filter check (SinceDate / BeforeDate)
                        if (!options.IsDateAllowed(msgDate))
                        {
                            job.SkippedMessages++;
                            progress?.Report(job);
                            continue;
                        }

                        var fp = BuildFingerprint(msgDate, summary.Envelope?.Subject, summary.Envelope?.From?.ToString());
                        var syntheticId = GenerateSyntheticMessageId(msgDate, summary.Envelope?.Subject, summary.Envelope?.From?.ToString());

                        // Multi-tier deduplication check:
                        // Tier 1: Normalized Message-ID match
                        // Tier 2: Composite Fallback Fingerprint match (Date + From + Subject)
                        // Tier 3: Synthetic Message-ID match
                        bool isDuplicate = false;

                        if (options.Deduplicate)
                        {
                            if (normMsgId != null && existingMessageIds.Contains(normMsgId))
                            {
                                isDuplicate = true;
                            }
                            else if (fp != null && existingFingerprints.Contains(fp))
                            {
                                isDuplicate = true;
                            }
                            else if (syntheticId != null && existingMessageIds.Contains(syntheticId))
                            {
                                isDuplicate = true;
                            }
                        }

                        if (isDuplicate)
                        {
                            job.SkippedMessages++;
                            progress?.Report(job);
                            continue;
                        }

                        try
                        {
                            // Fetch message
                            var message = await srcFolder.GetMessageAsync(summary.UniqueId, ct);

                            // If message lacks Message-ID header, inject deterministic synthetic ID
                            if (string.IsNullOrWhiteSpace(message.MessageId))
                            {
                                message.MessageId = syntheticId ?? $"{Guid.NewGuid():N}@mundofy.migrated";
                            }

                            // Filter flags
                            var flags = FilterFlags(summary.Flags ?? MessageFlags.None, options);
                            var internalDate = summary.InternalDate ?? summary.Envelope?.Date ?? DateTimeOffset.Now;

                            await dstFolder.AppendAsync(message, flags, internalDate, ct);

                            var recordedId = NormalizeMessageId(message.MessageId);
                            if (recordedId != null)
                                existingMessageIds.Add(recordedId);
                            if (fp != null)
                                existingFingerprints.Add(fp);

                            job.CopiedMessages++;
                            long msgBytes = summary.Size ?? 1024;
                            job.BytesTransferred += msgBytes;
                            job.TransferSpeed = CalculateSpeed(job.BytesTransferred, stopwatch.Elapsed);

                            Log(LogLevel.Info, $"[{job.SourceUser}] Copied msg #{i + 1} in '{srcFolder.FullName}': Subject='{summary.Envelope?.Subject}', Date='{msgDate:yyyy-MM-dd HH:mm}', MsgId='{rawMsgId ?? "(Generated: " + message.MessageId + ")"}'");
                        }
                        catch (Exception ex)
                        {
                            job.FailedMessages++;
                            Log(LogLevel.Warning, $"[{job.SourceUser}] Error copying msg #{i + 1} in '{srcFolder.FullName}': {ex.Message}");
                        }

                        if (i % 5 == 0 || i == summaries.Count - 1)
                        {
                            job.StatusMessage = $"Copying '{srcFolder.FullName}' ({i + 1}/{summaries.Count})";
                            progress?.Report(job);
                        }
                    }
                }
                finally
                {
                    if (dstFolder.IsOpen)
                    {
                        await dstFolder.CloseAsync(false, ct);
                    }
                }
            }
            finally
            {
                if (srcFolder.IsOpen)
                {
                    await srcFolder.CloseAsync(false, ct);
                }
            }
        }

        await srcClient.DisconnectAsync(true, ct);
        await dstClient.DisconnectAsync(true, ct);
    }

    private async Task MigratePop3ToImapAsync(
        ServerEndpoint source,
        ServerEndpoint dest,
        AccountJob job,
        MigrationOptions options,
        Stopwatch stopwatch,
        IProgress<AccountJob>? progress,
        CancellationToken ct)
    {
        using var popClient = CreatePop3Client(source);
        using var imapClient = CreateImapClient(dest);

        job.StatusMessage = "Connecting to POP3 source...";
        progress?.Report(job);
        var (srcHost, srcPort) = ResolveHostAndPort(source);
        await popClient.ConnectAsync(srcHost, srcPort, GetSecureSocketOptions(source), ct);
        await popClient.AuthenticateAsync(job.SourceUser, job.SourcePassword, ct);

        job.StatusMessage = "Connecting to destination IMAP...";
        progress?.Report(job);
        var (dstHost, dstPort) = ResolveHostAndPort(dest);
        await imapClient.ConnectAsync(dstHost, dstPort, GetSecureSocketOptions(dest), ct);
        string dstToken = !string.IsNullOrWhiteSpace(job.DestOAuthToken) ? job.DestOAuthToken : job.DestPassword;
        await AuthenticateImapClientAsync(imapClient, dest, job.DestUser, dstToken, ct);

        int count = popClient.Count;
        job.TotalMessages = count;
        job.CurrentFolder = "INBOX (POP3)";
        progress?.Report(job);

        IMailFolder dstFolder = imapClient.Inbox;
        if (!string.IsNullOrWhiteSpace(options.DstRootFolder))
        {
            var personal = imapClient.PersonalNamespaces.FirstOrDefault();
            var root = personal != null ? imapClient.GetFolder(personal) : imapClient.Inbox;
            try 
            { 
                var created = await root.CreateAsync(options.DstRootFolder, true, ct); 
                if (created != null) dstFolder = created;
            }
            catch 
            { 
                try { dstFolder = await imapClient.GetFolderAsync(options.DstRootFolder, ct); }
                catch { dstFolder = imapClient.Inbox; }
            }
        }

        dstFolder ??= imapClient.Inbox;
        await dstFolder.OpenAsync(FolderAccess.ReadWrite, ct);

        // Preload Message-IDs and fingerprints for deduplication
        var existingMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (options.Deduplicate && dstFolder.Count > 0)
        {
            var dstItems = MessageSummaryItems.Envelope |
                           MessageSummaryItems.InternalDate |
                           MessageSummaryItems.Size;
            var dstSummaries = await dstFolder.FetchAsync(0, -1, dstItems, ct);
            foreach (var s in dstSummaries)
            {
                var normId = NormalizeMessageId(s.Envelope?.MessageId);
                if (normId != null)
                    existingMessageIds.Add(normId);

                var fp = BuildFingerprint(s.Envelope?.Date ?? s.InternalDate, s.Envelope?.Subject, s.Envelope?.From?.ToString());
                if (fp != null)
                    existingFingerprints.Add(fp);
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var message = await popClient.GetMessageAsync(i, ct);

                // Date range filter check (SinceDate / BeforeDate)
                if (!options.IsDateAllowed(message.Date))
                {
                    job.SkippedMessages++;
                    progress?.Report(job);
                    continue;
                }

                var normMsgId = NormalizeMessageId(message.MessageId);
                var fp = BuildFingerprint(message.Date, message.Subject, message.From?.ToString());
                var syntheticId = GenerateSyntheticMessageId(message.Date, message.Subject, message.From?.ToString());

                bool isDuplicate = false;
                if (options.Deduplicate)
                {
                    if (normMsgId != null && existingMessageIds.Contains(normMsgId))
                        isDuplicate = true;
                    else if (fp != null && existingFingerprints.Contains(fp))
                        isDuplicate = true;
                    else if (syntheticId != null && existingMessageIds.Contains(syntheticId))
                        isDuplicate = true;
                }

                if (isDuplicate)
                {
                    job.SkippedMessages++;
                    progress?.Report(job);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(message.MessageId))
                {
                    message.MessageId = syntheticId ?? $"{Guid.NewGuid():N}@mundofy.migrated";
                }

                await dstFolder.AppendAsync(message, MessageFlags.Seen, message.Date, ct);

                var recordedId = NormalizeMessageId(message.MessageId);
                if (recordedId != null)
                    existingMessageIds.Add(recordedId);
                if (fp != null)
                    existingFingerprints.Add(fp);

                job.CopiedMessages++;
                job.BytesTransferred += 2048; // estimated bytes if not sized
                job.TransferSpeed = CalculateSpeed(job.BytesTransferred, stopwatch.Elapsed);

                Log(LogLevel.Info, $"[{job.SourceUser}] Copied POP3 msg #{i + 1}: Subject='{message.Subject}', Date='{message.Date:yyyy-MM-dd HH:mm}', MsgId='{message.MessageId}'");
            }
            catch (Exception ex)
            {
                job.FailedMessages++;
                Log(LogLevel.Warning, $"[{job.SourceUser}] Error copying POP3 msg #{i + 1}: {ex.Message}");
            }

            if (i % 5 == 0 || i == count - 1)
            {
                job.StatusMessage = $"Copying POP3 messages ({i + 1}/{count})";
                progress?.Report(job);
            }
        }

        await popClient.DisconnectAsync(true, ct);
        await imapClient.DisconnectAsync(true, ct);
    }

    private static async Task<List<IMailFolder>> GetAllSubfoldersAsync(IMailFolder folder, CancellationToken ct)
    {
        var list = new List<IMailFolder>();
        if (!string.IsNullOrEmpty(folder.FullName))
            list.Add(folder);

        try
        {
            var subfolders = await folder.GetSubfoldersAsync(false, ct);
            foreach (var sub in subfolders)
            {
                list.AddRange(await GetAllSubfoldersAsync(sub, ct));
            }
        }
        catch { }

        return list;
    }

    private static async Task<IMailFolder> ResolveOrCreateDestinationFolderAsync(
        ImapClient dstClient,
        IMailFolder srcFolder,
        MigrationOptions options,
        CancellationToken ct)
    {
        if (options.CopyAllMessagesToInbox || srcFolder.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return dstClient.Inbox;
        }

        string targetName = srcFolder.FullName;
        if (targetName.StartsWith("INBOX.", StringComparison.OrdinalIgnoreCase))
            targetName = targetName.Substring(6);
        else if (targetName.StartsWith("INBOX/", StringComparison.OrdinalIgnoreCase))
            targetName = targetName.Substring(6);

        if (!string.IsNullOrWhiteSpace(options.DstRootFolder))
            targetName = $"{options.DstRootFolder}/{targetName}";

        var personal = dstClient.PersonalNamespaces.FirstOrDefault();
        var root = personal != null ? dstClient.GetFolder(personal) : dstClient.Inbox;

        try
        {
            return await dstClient.GetFolderAsync(targetName, ct);
        }
        catch
        {
            try
            {
                var created = await root.CreateAsync(targetName, true, ct);
                if (created != null) return created;
            }
            catch { }

            try
            {
                return await dstClient.GetFolderAsync(targetName, ct);
            }
            catch
            {
                return dstClient.Inbox;
            }
        }
    }

    private static MessageFlags FilterFlags(MessageFlags originalFlags, MigrationOptions options)
    {
        return originalFlags & ~MessageFlags.Recent;
    }

    private static ImapClient CreateImapClient(ServerEndpoint endpoint)
    {
        var client = new ImapClient();
        if (endpoint.AllowInvalidCertificates)
        {
            client.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
        }
        client.Timeout = 60000;
        return client;
    }

    private static Pop3Client CreatePop3Client(ServerEndpoint endpoint)
    {
        var client = new Pop3Client();
        if (endpoint.AllowInvalidCertificates)
        {
            client.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
        }
        client.Timeout = 60000;
        return client;
    }

    public static (string Host, int Port) ResolveHostAndPort(ServerEndpoint endpoint)
    {
        string host = endpoint.Host?.Trim() ?? string.Empty;
        int port = endpoint.Port;

        if (string.IsNullOrWhiteSpace(host))
        {
            if (endpoint.Protocol == ServerProtocol.Microsoft365)
            {
                host = "outlook.office365.com";
                port = 993;
            }
            else if (endpoint.Protocol == ServerProtocol.GoogleWorkspace)
            {
                host = "imap.gmail.com";
                port = 993;
            }
        }

        return (host, port <= 0 ? 993 : port);
    }

    private static async Task AuthenticateImapClientAsync(
        ImapClient client,
        ServerEndpoint endpoint,
        string username,
        string passwordOrToken,
        CancellationToken ct)
    {
        string token = !string.IsNullOrWhiteSpace(endpoint.OAuthAccessToken)
            ? endpoint.OAuthAccessToken
            : (passwordOrToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase) ? passwordOrToken[6..] : "");

        if (endpoint.IsOAuth2 || !string.IsNullOrWhiteSpace(token))
        {
            var authToken = !string.IsNullOrWhiteSpace(token) ? token : passwordOrToken;
            var oauth2 = new SaslMechanismOAuth2(username, authToken);
            await client.AuthenticateAsync(oauth2, ct);
        }
        else
        {
            await client.AuthenticateAsync(username, passwordOrToken, ct);
        }
    }

    private static SecureSocketOptions GetSecureSocketOptions(ServerEndpoint endpoint)
    {
        if (!endpoint.UseSsl)
        {
            return SecureSocketOptions.StartTlsWhenAvailable;
        }
        return endpoint.Port is 993 or 995 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    private static string CalculateSpeed(long bytes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1) return "Calculating...";
        double mbps = (bytes / 1024.0 / 1024.0) / elapsed.TotalSeconds;
        return $"{mbps:F1} MB/s";
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < suffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return $"{d:0.##} {suffixes[i]}";
    }

    private static string? NormalizeMessageId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var trimmed = id.Trim().Trim('<', '>').Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static string? BuildFingerprint(DateTimeOffset? date, string? subject, string? from)
    {
        if (!date.HasValue && string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(from))
            return null;

        var dateStr = date.HasValue ? date.Value.ToUniversalTime().ToString("yyyyMMddHHmm") : "nodate";
        var cleanSubj = string.IsNullOrWhiteSpace(subject) ? "nosubj" : subject.Trim().ToLowerInvariant();
        var cleanFrom = string.IsNullOrWhiteSpace(from) ? "nofrom" : from.Trim().ToLowerInvariant();

        return $"{dateStr}|{cleanFrom}|{cleanSubj}";
    }

    private static string? GenerateSyntheticMessageId(DateTimeOffset? date, string? subject, string? from)
    {
        var fp = BuildFingerprint(date, subject, from);
        if (fp == null) return null;

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fp)))[..16].ToLowerInvariant();
        return $"{hash}@mundofy.migrated";
    }
}
