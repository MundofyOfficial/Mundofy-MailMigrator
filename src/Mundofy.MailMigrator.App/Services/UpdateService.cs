using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

using Mundofy.MailMigrator.Core;

namespace Mundofy.MailMigrator.App.Services;

public record UpdateInfo(
    bool HasUpdate,
    string LatestVersion,
    string CurrentVersion,
    string DownloadUrl,
    string ReleaseNotes,
    long FileSize
);

public static class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/MundofyOfficial/Mundofy-MailMigrator/releases/latest";
    private static readonly HttpClient HttpClient = new();

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mundofy-MailMigrator", AppVersion.Current));
        HttpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public static string GetCurrentVersion()
    {
        return AppVersion.Current;
    }

    public static async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var currentVersionStr = GetCurrentVersion();
        try
        {
            var response = await HttpClient.GetStringAsync(GitHubApiUrl, ct);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            var releaseNotes = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
            var latestVersionStr = tagName.TrimStart('v', 'V').Trim();

            if (string.IsNullOrWhiteSpace(latestVersionStr))
            {
                return new UpdateInfo(false, currentVersionStr, currentVersionStr, "", "", 0);
            }

            // Find MundofyMailMigrator.exe asset
            string downloadUrl = "";
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElem.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.Equals("MundofyMailMigrator.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                        fileSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        break;
                    }
                }
            }

            bool hasUpdate = IsNewerVersion(latestVersionStr, currentVersionStr);
            return new UpdateInfo(hasUpdate, latestVersionStr, currentVersionStr, downloadUrl, releaseNotes, fileSize);
        }
        catch
        {
            return new UpdateInfo(false, currentVersionStr, currentVersionStr, "", "", 0);
        }
    }

    private static bool IsNewerVersion(string latestStr, string currentStr)
    {
        if (Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current))
        {
            return latest > current;
        }

        // Fallback simple string comparison
        return string.Compare(latestStr, currentStr, StringComparison.OrdinalIgnoreCase) > 0;
    }

    public static async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
        {
            currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
        {
            throw new InvalidOperationException("Cannot determine executable path of the running application.");
        }

        var dir = Path.GetDirectoryName(currentExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        var updateExePath = Path.Combine(dir, "MundofyMailMigrator.update.exe");

        // 1. Download file with progress
        using (var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(updateExePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                if (totalBytes > 0)
                {
                    progress?.Report((double)totalRead / totalBytes * 100.0);
                }
            }
        }

        // 2. Create detached restart swap script
        var scriptPath = Path.Combine(Path.GetTempPath(), $"mundofy_updater_{Guid.NewGuid():N}.cmd");
        var scriptContent = $@"@echo off
timeout /t 1 /nobreak > NUL
:retry
move /y ""{updateExePath}"" ""{currentExePath}"" > NUL 2>&1
if errorlevel 1 (
    timeout /t 1 /nobreak > NUL
    goto retry
)
start """" ""{currentExePath}""
del ""%~f0""
";
        await File.WriteAllTextAsync(scriptPath, scriptContent, ct);

        // 3. Launch detached batch process and shut down current app
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{scriptPath}\"\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        Process.Start(psi);
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }
}
