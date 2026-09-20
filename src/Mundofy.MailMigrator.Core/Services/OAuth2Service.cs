using System.Diagnostics;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Identity.Client;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.Core.Services;

public class OAuthTokenResult
{
    public bool Success { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string AccountUsername => AccountEmail;
    public DateTimeOffset ExpiresOn { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class OAuth2Service
{
    // Well-known Thunderbird client id with standard public consent for IMAP/POP/SMTP in M365
    public const string DefaultMicrosoftClientId = "08162f7c-0fd2-4204-a510-dff2e0340b16";
    public const string DefaultMicrosoftTenant = "common";
    public const string MicrosoftImapScope = "https://outlook.office365.com/IMAP.AccessAsUser.All";

    // Default Google Cloud client credentials (can be supplied via environment variables or parameter overrides)
    public static string DefaultGoogleClientId => Environment.GetEnvironmentVariable("MUNDOFY_GOOGLE_CLIENT_ID") ?? string.Empty;
    public static string DefaultGoogleClientSecret => Environment.GetEnvironmentVariable("MUNDOFY_GOOGLE_CLIENT_SECRET") ?? string.Empty;

    // Standard scopes
    public static readonly string[] MicrosoftInteractiveScopes = new[]
    {
        MicrosoftImapScope,
        "offline_access"
    };

    public static readonly string[] GoogleImapScopes = new[]
    {
        "https://mail.google.com/"
    };

    /// <summary>
    /// Authenticates interactively with Microsoft 365 using default system browser and PKCE.
    /// </summary>
    public async Task<OAuthTokenResult> AuthenticateMicrosoftInteractiveAsync(
        string? clientId = null,
        string? tenantId = null,
        string? loginHint = null,
        CancellationToken ct = default)
    {
        try
        {
            var effectiveClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultMicrosoftClientId : clientId.Trim();
            var effectiveTenant = string.IsNullOrWhiteSpace(tenantId) ? DefaultMicrosoftTenant : tenantId.Trim();

            var builder = PublicClientApplicationBuilder.Create(effectiveClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, effectiveTenant)
                .WithRedirectUri("http://localhost");

            var app = builder.Build();

            var acquireToken = app.AcquireTokenInteractive(MicrosoftInteractiveScopes)
                .WithPrompt(Prompt.SelectAccount);

            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                acquireToken = acquireToken.WithLoginHint(loginHint);
            }

            var authResult = await acquireToken.ExecuteAsync(ct);

            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = authResult.AccessToken,
                AccountEmail = authResult.Account?.Username ?? loginHint ?? string.Empty,
                ExpiresOn = authResult.ExpiresOn
            };
        }
        catch (MsalException ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Microsoft authentication failed: {ex.Message} (Code: {ex.ErrorCode})"
            };
        }
        catch (Exception ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Microsoft authentication error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Authenticates with Microsoft 365 using Device Code Flow (displays a short code to enter at https://microsoft.com/devicelogin).
    /// Ideal for CLI, remote environments, or headless servers.
    /// </summary>
    public async Task<OAuthTokenResult> AuthenticateMicrosoftDeviceCodeAsync(
        Action<string, string> onDeviceCodePrompt,
        string? clientId = null,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        try
        {
            var effectiveClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultMicrosoftClientId : clientId.Trim();
            var effectiveTenant = string.IsNullOrWhiteSpace(tenantId) ? DefaultMicrosoftTenant : tenantId.Trim();

            var app = PublicClientApplicationBuilder.Create(effectiveClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, effectiveTenant)
                .Build();

            var authResult = await app.AcquireTokenWithDeviceCode(MicrosoftInteractiveScopes, deviceCodeResult =>
            {
                onDeviceCodePrompt?.Invoke(deviceCodeResult.UserCode, deviceCodeResult.VerificationUrl);
                return Task.CompletedTask;
            }).ExecuteAsync(ct);

            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = authResult.AccessToken,
                AccountEmail = authResult.Account?.Username ?? string.Empty,
                ExpiresOn = authResult.ExpiresOn
            };
        }
        catch (Exception ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Device code authentication failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Authenticates with Microsoft 365 as an Enterprise Tenant Administrator using Azure App Registration Client Credentials.
    /// Used for Batch Migrations to access any mailbox in the tenant without individual user passwords.
    /// </summary>
    public async Task<OAuthTokenResult> AcquireMicrosoftTenantTokenAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.");
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client ID is required.");
            if (string.IsNullOrWhiteSpace(clientSecret)) throw new ArgumentException("Client Secret is required.");

            var app = ConfidentialClientApplicationBuilder.Create(clientId.Trim())
                .WithClientSecret(clientSecret.Trim())
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId.Trim()}"))
                .Build();

            var scopes = new[] { "https://outlook.office365.com/.default" };
            var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync(ct);

            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = authResult.AccessToken,
                ExpiresOn = authResult.ExpiresOn
            };
        }
        catch (Exception ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Tenant Client Credentials authentication failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Authenticates interactively with Google Workspace / Gmail using OAuth 2.0 PKCE localhost loopback.
    /// </summary>
    public async Task<OAuthTokenResult> AuthenticateGoogleInteractiveAsync(
        string? clientId = null,
        string? clientSecret = null,
        string? loginHint = null,
        CancellationToken ct = default)
    {
        try
        {
            var effectiveClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultGoogleClientId : clientId.Trim();
            var effectiveClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? DefaultGoogleClientSecret : clientSecret.Trim();

            if (string.IsNullOrWhiteSpace(effectiveClientId) || string.IsNullOrWhiteSpace(effectiveClientSecret))
            {
                return new OAuthTokenResult
                {
                    Success = false,
                    ErrorMessage = "Google Cloud OAuth Client ID & Secret are not configured. Please use a Google App Password for single migration, Service Account JSON for batch, or set MUNDOFY_GOOGLE_CLIENT_ID / MUNDOFY_GOOGLE_CLIENT_SECRET."
                };
            }

            var secrets = new ClientSecrets
            {
                ClientId = effectiveClientId,
                ClientSecret = effectiveClientSecret
            };

            var userKey = string.IsNullOrWhiteSpace(loginHint) ? "user" : loginHint.Trim();
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                GoogleImapScopes,
                userKey,
                ct
            );

            var token = await credential.GetAccessTokenForRequestAsync(cancellationToken: ct);

            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = token,
                AccountEmail = loginHint ?? string.Empty,
                ExpiresOn = credential.Token.IssuedUtc.AddSeconds(credential.Token.ExpiresInSeconds ?? 3600)
            };
        }
        catch (Exception ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Google authentication failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Authenticates with Google Workspace using a Service Account JSON key with Domain-Wide Delegation.
    /// Allows migrating all accounts in a Google Workspace domain without individual user passwords.
    /// </summary>
    public async Task<OAuthTokenResult> AcquireGoogleServiceAccountTokenAsync(
        string serviceAccountJsonContentOrPath,
        string targetUserEmail,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceAccountJsonContentOrPath))
                throw new ArgumentException("Service account JSON or path is required.");
            if (string.IsNullOrWhiteSpace(targetUserEmail))
                throw new ArgumentException("Target user email is required for impersonation.");

            string json = serviceAccountJsonContentOrPath.Trim();
            if (File.Exists(json))
            {
                json = await File.ReadAllTextAsync(json, ct);
            }

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
#pragma warning disable CS0618
            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped(GoogleImapScopes)
                .CreateWithUser(targetUserEmail.Trim());
#pragma warning restore CS0618

            var token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync(cancellationToken: ct);

            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = token,
                AccountEmail = targetUserEmail,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(55)
            };
        }
        catch (Exception ex)
        {
            return new OAuthTokenResult
            {
                Success = false,
                ErrorMessage = $"Google Service Account delegation failed for [{targetUserEmail}]: {ex.Message}"
            };
        }
    }
}
