namespace Mundofy.MailMigrator.Core.Models;

public enum ServerProtocol
{
    Imap,
    Pop3,
    Microsoft365,
    GoogleWorkspace
}

public class ServerEndpoint
{
    public ServerProtocol Protocol { get; set; } = ServerProtocol.Imap;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public bool AllowInvalidCertificates { get; set; } = false;
    public string RootFolder { get; set; } = string.Empty;

    // Modern OAuth2 & Tenant Configurations
    public string OAuthClientId { get; set; } = string.Empty;
    public string OAuthTenantId { get; set; } = string.Empty;
    public string OAuthClientSecret { get; set; } = string.Empty;
    public string GoogleServiceAccountJson { get; set; } = string.Empty;
    public string OAuthAccessToken { get; set; } = string.Empty;
    public string OAuthRefreshToken { get; set; } = string.Empty;
    public DateTimeOffset? OAuthTokenExpiresAt { get; set; }

    private bool? _isOAuth2;
    public bool IsOAuth2
    {
        get => _isOAuth2 ?? (Protocol == ServerProtocol.Microsoft365 || Protocol == ServerProtocol.GoogleWorkspace || !string.IsNullOrWhiteSpace(OAuthAccessToken));
        set => _isOAuth2 = value;
    }

    public ServerEndpoint Clone()
    {
        return new ServerEndpoint
        {
            Protocol = this.Protocol,
            Host = this.Host,
            Port = this.Port,
            UseSsl = this.UseSsl,
            AllowInvalidCertificates = this.AllowInvalidCertificates,
            RootFolder = this.RootFolder,
            OAuthClientId = this.OAuthClientId,
            OAuthTenantId = this.OAuthTenantId,
            OAuthClientSecret = this.OAuthClientSecret,
            GoogleServiceAccountJson = this.GoogleServiceAccountJson,
            OAuthAccessToken = this.OAuthAccessToken,
            OAuthRefreshToken = this.OAuthRefreshToken,
            OAuthTokenExpiresAt = this.OAuthTokenExpiresAt
        };
    }
}
