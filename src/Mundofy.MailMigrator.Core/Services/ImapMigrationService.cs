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

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        ServerEndpoint endpoint,
        string username,
        string password,
        CancellationToken ct = default)
    {
        try
        {
            if (endpoint.Protocol == ServerProtocol.Pop3)
            {
                using var client = CreatePop3Client(endpoint);
                await client.ConnectAsync(endpoint.Host, endpoint.Port, GetSecureSocketOptions(endpoint), ct);
                await client.AuthenticateAsync(username, password, ct);
                int count = client.Count;
                await client.DisconnectAsync(true, ct);
                return (true, $"POP3 Connected successfully! ({count} messages in mailbox)");
            }
            else
            {
                using var client = CreateImapClient(endpoint);
                await client.ConnectAsync(endpoint.Host, endpoint.Port, GetSecureSocketOptions(endpoint), ct);
                await client.AuthenticateAsync(username, password, ct);
                await client.DisconnectAsync(true, ct);
                return (true, "IMAP Connected successfully!");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
        await srcClient.ConnectAsync(source.Host, source.Port, GetSecureSocketOptions(source), ct);
        await srcClient.AuthenticateAsync(job.SourceUser, job.SourcePassword, ct);

        job.StatusMessage = "Connecting to destination IMAP server...";
        progress?.Report(job);
        await dstClient.ConnectAsync(dest.Host, dest.Port, GetSecureSocketOptions(dest), ct);
        await dstClient.AuthenticateAsync(job.DestUser, job.DestPassword, ct);

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
                    // Deduplication: retrieve existing Message-IDs from destination folder
                    var existingMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (options.Deduplicate && dstFolder.Count > 0)
                    {
                        job.StatusMessage = $"Checking for existing messages in '{dstFolder.FullName}'...";
                        progress?.Report(job);
                        var dstSummaries = await dstFolder.FetchAsync(0, -1, MessageSummaryItems.Envelope, ct);
                        foreach (var s in dstSummaries)
                        {
                            if (!string.IsNullOrEmpty(s.Envelope?.MessageId))
                                existingMessageIds.Add(s.Envelope.MessageId);
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
                        var msgId = summary.Envelope?.MessageId;

                        // Deduplication check: email already exists on destination
                        if (options.Deduplicate && !string.IsNullOrEmpty(msgId) && existingMessageIds.Contains(msgId))
                        {
                            job.SkippedMessages++;
                            progress?.Report(job);
                            continue;
                        }

                        try
                        {
                            // Fetch message
                            var message = await srcFolder.GetMessageAsync(summary.UniqueId, ct);

                            // Filter flags
                            var flags = FilterFlags(summary.Flags ?? MessageFlags.None, options);
                            var internalDate = summary.InternalDate ?? DateTimeOffset.Now;

                            await dstFolder.AppendAsync(message, flags, internalDate, ct);

                            if (!string.IsNullOrEmpty(msgId))
                                existingMessageIds.Add(msgId);

                            job.CopiedMessages++;
                            long msgBytes = summary.Size ?? 1024;
                            job.BytesTransferred += msgBytes;
                            job.TransferSpeed = CalculateSpeed(job.BytesTransferred, stopwatch.Elapsed);
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
        await popClient.ConnectAsync(source.Host, source.Port, GetSecureSocketOptions(source), ct);
        await popClient.AuthenticateAsync(job.SourceUser, job.SourcePassword, ct);

        job.StatusMessage = "Connecting to destination IMAP...";
        progress?.Report(job);
        await imapClient.ConnectAsync(dest.Host, dest.Port, GetSecureSocketOptions(dest), ct);
        await imapClient.AuthenticateAsync(job.DestUser, job.DestPassword, ct);

        int count = popClient.Count;
        job.TotalMessages = count;
        job.CurrentFolder = "INBOX (POP3)";
        progress?.Report(job);

        IMailFolder dstFolder = imapClient.Inbox;
        if (!string.IsNullOrWhiteSpace(options.DstRootFolder))
        {
            var personal = imapClient.PersonalNamespaces.FirstOrDefault();
            var root = personal != null ? imapClient.GetFolder(personal) : imapClient.Inbox;
            try { dstFolder = await root.CreateAsync(options.DstRootFolder, true, ct); }
            catch { dstFolder = imapClient.GetFolder(options.DstRootFolder); }
        }

        await dstFolder.OpenAsync(FolderAccess.ReadWrite, ct);

        // Preload Message-IDs for deduplication
        var existingMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (options.Deduplicate && dstFolder.Count > 0)
        {
            var dstSummaries = await dstFolder.FetchAsync(0, -1, MessageSummaryItems.Envelope, ct);
            foreach (var s in dstSummaries)
            {
                if (!string.IsNullOrEmpty(s.Envelope?.MessageId))
                    existingMessageIds.Add(s.Envelope.MessageId);
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var message = await popClient.GetMessageAsync(i, ct);
                if (options.Deduplicate && !string.IsNullOrEmpty(message.MessageId) && existingMessageIds.Contains(message.MessageId))
                {
                    job.SkippedMessages++;
                    progress?.Report(job);
                    continue;
                }

                await dstFolder.AppendAsync(message, MessageFlags.Seen, message.Date, ct);

                if (!string.IsNullOrEmpty(message.MessageId))
                    existingMessageIds.Add(message.MessageId);

                job.CopiedMessages++;
                job.BytesTransferred += 2048; // estimated bytes if not sized
                job.TransferSpeed = CalculateSpeed(job.BytesTransferred, stopwatch.Elapsed);
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
                return await root.CreateAsync(targetName, true, ct);
            }
            catch
            {
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
}
