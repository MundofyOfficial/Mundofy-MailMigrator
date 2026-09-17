using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Services;

public class ServerAutoDiscoveryResult
{
    public bool Success { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public ServerProtocol Protocol { get; set; } = ServerProtocol.Imap;
    public string Provider { get; set; } = string.Empty;
    public string DetectionSource { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class ServerAutoDiscoveryService
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        AllowAutoRedirect = true
    })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private static readonly Dictionary<string, (string Host, int Port, bool UseSsl, string Provider)> WellKnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        // Google / Gmail
        { "gmail.com", ("imap.gmail.com", 993, true, "Google Workspace / Gmail") },
        { "googlemail.com", ("imap.gmail.com", 993, true, "Google Workspace / Gmail") },

        // Microsoft / Outlook / 365
        { "outlook.com", ("outlook.office365.com", 993, true, "Microsoft 365 / Outlook") },
        { "hotmail.com", ("outlook.office365.com", 993, true, "Microsoft Hotmail") },
        { "live.com", ("outlook.office365.com", 993, true, "Microsoft Live") },
        { "msn.com", ("outlook.office365.com", 993, true, "Microsoft MSN") },
        { "office365.com", ("outlook.office365.com", 993, true, "Microsoft 365") },

        // Yahoo
        { "yahoo.com", ("imap.mail.yahoo.com", 993, true, "Yahoo Mail") },
        { "ymail.com", ("imap.mail.yahoo.com", 993, true, "Yahoo Mail") },
        { "rocketmail.com", ("imap.mail.yahoo.com", 993, true, "Yahoo Mail") },

        // Apple iCloud
        { "icloud.com", ("imap.mail.me.com", 993, true, "Apple iCloud") },
        { "me.com", ("imap.mail.me.com", 993, true, "Apple MobileMe") },
        { "mac.com", ("imap.mail.me.com", 993, true, "Apple Mac.com") },

        // Fastmail
        { "fastmail.com", ("imap.fastmail.com", 993, true, "Fastmail") },
        { "fastmail.fm", ("imap.fastmail.com", 993, true, "Fastmail") },

        // Zoho
        { "zoho.com", ("imap.zoho.com", 993, true, "Zoho Mail") },
        { "zoho.eu", ("imap.zoho.eu", 993, true, "Zoho Mail (EU)") },

        // GMX / Web.de
        { "gmx.net", ("imap.gmx.net", 993, true, "GMX Mail") },
        { "gmx.com", ("imap.gmx.com", 993, true, "GMX Mail") },
        { "gmx.de", ("imap.gmx.net", 993, true, "GMX Mail") },
        { "web.de", ("imap.web.de", 993, true, "Web.de") },

        // Mail.ru / Yandex
        { "mail.ru", ("imap.mail.ru", 993, true, "Mail.ru") },
        { "inbox.ru", ("imap.mail.ru", 993, true, "Mail.ru") },
        { "list.ru", ("imap.mail.ru", 993, true, "Mail.ru") },
        { "bk.ru", ("imap.mail.ru", 993, true, "Mail.ru") },
        { "yandex.com", ("imap.yandex.com", 993, true, "Yandex Mail") },
        { "yandex.ru", ("imap.yandex.ru", 993, true, "Yandex Mail") },

        // Proton (Bridge)
        { "protonmail.com", ("127.0.0.1", 1143, true, "Proton Mail (Bridge)") },
        { "proton.me", ("127.0.0.1", 1143, true, "Proton Mail (Bridge)") },
    };

    public async Task<ServerAutoDiscoveryResult> DiscoverAsync(string emailOrDomain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(emailOrDomain))
        {
            return new ServerAutoDiscoveryResult
            {
                Success = false,
                ErrorMessage = "Email or domain cannot be empty."
            };
        }

        string domain = ExtractDomain(emailOrDomain);
        if (string.IsNullOrEmpty(domain) || !domain.Contains('.'))
        {
            return new ServerAutoDiscoveryResult
            {
                Success = false,
                ErrorMessage = "Invalid email or domain format."
            };
        }

        // STAGE 1: Well-Known Public Provider Registry (0ms instant lookup)
        if (WellKnownProviders.TryGetValue(domain, out var known))
        {
            return new ServerAutoDiscoveryResult
            {
                Success = true,
                Host = known.Host,
                Port = known.Port,
                UseSsl = known.UseSsl,
                Protocol = ServerProtocol.Imap,
                Provider = known.Provider,
                DetectionSource = "Well-Known Provider Registry"
            };
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(3.5)); // Total budget cap

        // STAGE 2: DNS MX Fingerprinting (Detects Google Workspace / M365 on custom domains)
        try
        {
            var mxResult = await ProbeMxFingerprintAsync(domain, linkedCts.Token);
            if (mxResult != null)
            {
                return mxResult;
            }
        }
        catch
        {
            // Proceed to next stage
        }

        // STAGE 3: Mozilla Thunderbird ISPDB (Global Open-Source Autoconfig Database)
        try
        {
            var ispdbResult = await ProbeMozillaIspdbAsync(domain, linkedCts.Token);
            if (ispdbResult != null)
            {
                return ispdbResult;
            }
        }
        catch
        {
            // Proceed to next stage
        }

        // STAGE 4: Domain Autoconfig XML (cPanel, Plesk, DirectAdmin)
        try
        {
            var autoconfigResult = await ProbeDomainAutoconfigXmlAsync(domain, linkedCts.Token);
            if (autoconfigResult != null)
            {
                return autoconfigResult;
            }
        }
        catch
        {
            // Proceed to next stage
        }

        // STAGE 5: Smart Socket Probe (imap.domain:993, mail.domain:993)
        try
        {
            var socketResult = await ProbeSocketsAsync(domain, linkedCts.Token);
            if (socketResult != null)
            {
                return socketResult;
            }
        }
        catch
        {
            // Fallback
        }

        // STAGE 6: Standard RFC / Convention Fallback
        return new ServerAutoDiscoveryResult
        {
            Success = true,
            Host = $"imap.{domain}",
            Port = 993,
            UseSsl = true,
            Protocol = ServerProtocol.Imap,
            Provider = domain,
            DetectionSource = "Default Convention Fallback (imap.{domain}:993)"
        };
    }

    private static string ExtractDomain(string input)
    {
        input = input.Trim();
        int atIndex = input.IndexOf('@');
        if (atIndex >= 0 && atIndex < input.Length - 1)
        {
            return input[(atIndex + 1)..].Trim().ToLowerInvariant();
        }
        return input.ToLowerInvariant();
    }

    private static async Task<ServerAutoDiscoveryResult?> ProbeMxFingerprintAsync(string domain, CancellationToken ct)
    {
        string url = $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(domain)}&type=MX";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Accept", "application/dns-json");

        using var response = await _httpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("Answer", out var answerArray) || answerArray.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var answer in answerArray.EnumerateArray())
        {
            if (answer.TryGetProperty("data", out var dataProp))
            {
                string data = dataProp.GetString()?.ToLowerInvariant() ?? "";
                
                // Google Workspace MX
                if (data.Contains("google.com") || data.Contains("googlemail.com") || data.Contains("aspmx.l.google.com"))
                {
                    return new ServerAutoDiscoveryResult
                    {
                        Success = true,
                        Host = "imap.gmail.com",
                        Port = 993,
                        UseSsl = true,
                        Protocol = ServerProtocol.Imap,
                        Provider = "Google Workspace",
                        DetectionSource = "DNS MX Fingerprint (Google Workspace)"
                    };
                }

                // Microsoft 365 MX
                if (data.Contains("outlook.com") || data.Contains("mail.protection.outlook.com"))
                {
                    return new ServerAutoDiscoveryResult
                    {
                        Success = true,
                        Host = "outlook.office365.com",
                        Port = 993,
                        UseSsl = true,
                        Protocol = ServerProtocol.Imap,
                        Provider = "Microsoft 365 / Exchange Online",
                        DetectionSource = "DNS MX Fingerprint (Microsoft 365)"
                    };
                }
            }
        }

        return null;
    }

    private static async Task<ServerAutoDiscoveryResult?> ProbeMozillaIspdbAsync(string domain, CancellationToken ct)
    {
        string url = $"https://autoconfig.thunderbird.net/v1.1/{Uri.EscapeDataString(domain)}";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseAutoconfigXml(xml, "Mozilla Thunderbird ISPDB");
    }

    private static async Task<ServerAutoDiscoveryResult?> ProbeDomainAutoconfigXmlAsync(string domain, CancellationToken ct)
    {
        var urls = new[]
        {
            $"https://autoconfig.{domain}/mail/config-v1.1.xml",
            $"https://{domain}/.well-known/autoconfig/mail/config-v1.1.xml",
            $"http://autoconfig.{domain}/mail/config-v1.1.xml"
        };

        foreach (var url in urls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var xml = await response.Content.ReadAsStringAsync(ct);
                    var result = ParseAutoconfigXml(xml, $"Domain Autoconfig ({url})");
                    if (result != null) return result;
                }
            }
            catch
            {
                // Continue to next URL
            }
        }

        return null;
    }

    private static ServerAutoDiscoveryResult? ParseAutoconfigXml(string xmlContent, string source)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return null;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var imapServer = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("incomingServer", StringComparison.OrdinalIgnoreCase) &&
                                     e.Attribute("type")?.Value.Equals("imap", StringComparison.OrdinalIgnoreCase) == true);

            if (imapServer != null)
            {
                string host = imapServer.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("hostname", StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
                string portStr = imapServer.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("port", StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "993";
                string socketType = imapServer.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("socketType", StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "SSL";

                if (!string.IsNullOrEmpty(host))
                {
                    int.TryParse(portStr, out int port);
                    if (port <= 0) port = 993;

                    bool useSsl = !socketType.Equals("plain", StringComparison.OrdinalIgnoreCase);

                    return new ServerAutoDiscoveryResult
                    {
                        Success = true,
                        Host = host,
                        Port = port,
                        UseSsl = useSsl,
                        Protocol = ServerProtocol.Imap,
                        Provider = host,
                        DetectionSource = source
                    };
                }
            }
        }
        catch
        {
            // Ignore malformed XML
        }

        return null;
    }

    private static async Task<ServerAutoDiscoveryResult?> ProbeSocketsAsync(string domain, CancellationToken ct)
    {
        var candidateHosts = new[]
        {
            $"imap.{domain}",
            $"mail.{domain}"
        };

        var tasks = candidateHosts.Select(host => TryProbeSslSocketAsync(host, 993, ct)).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            var host = await completedTask;
            if (!string.IsNullOrEmpty(host))
            {
                return new ServerAutoDiscoveryResult
                {
                    Success = true,
                    Host = host,
                    Port = 993,
                    UseSsl = true,
                    Protocol = ServerProtocol.Imap,
                    Provider = domain,
                    DetectionSource = $"Active SSL Probe ({host}:993)"
                };
            }
        }

        return null;
    }

    private static async Task<string?> TryProbeSslSocketAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port, ct).AsTask();

            var timeoutTask = Task.Delay(1500, ct);
            var finished = await Task.WhenAny(connectTask, timeoutTask);

            if (finished == connectTask && client.Connected)
            {
                using var stream = client.GetStream();
                using var sslStream = new SslStream(stream, false, (_, _, _, _) => true);
                
                using var authCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                authCts.CancelAfter(1500);

                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                }, authCts.Token);

                return host;
            }
        }
        catch
        {
            // Connection or SSL handshake failed
        }

        return null;
    }
}
