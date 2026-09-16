namespace Mundofy.MailMigrator.Core.Models;

public enum ServerProtocol
{
    Imap,
    Pop3
}

public class ServerEndpoint
{
    public ServerProtocol Protocol { get; set; } = ServerProtocol.Imap;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public bool AllowInvalidCertificates { get; set; } = false;
    public string RootFolder { get; set; } = string.Empty;

    public ServerEndpoint Clone()
    {
        return new ServerEndpoint
        {
            Protocol = this.Protocol,
            Host = this.Host,
            Port = this.Port,
            UseSsl = this.UseSsl,
            AllowInvalidCertificates = this.AllowInvalidCertificates,
            RootFolder = this.RootFolder
        };
    }
}
