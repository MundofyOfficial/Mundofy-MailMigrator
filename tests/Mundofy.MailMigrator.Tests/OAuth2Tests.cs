using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Services;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class OAuth2Tests
{
    [Fact]
    public void ResolveHostAndPort_DefaultsForMicrosoft365()
    {
        var ep = new ServerEndpoint
        {
            Protocol = ServerProtocol.Microsoft365,
            Host = "",
            Port = 0
        };

        var (host, port) = ImapMigrationService.ResolveHostAndPort(ep);

        Assert.Equal("outlook.office365.com", host);
        Assert.Equal(993, port);
    }

    [Fact]
    public void ResolveHostAndPort_DefaultsForGoogleWorkspace()
    {
        var ep = new ServerEndpoint
        {
            Protocol = ServerProtocol.GoogleWorkspace,
            Host = "",
            Port = 0
        };

        var (host, port) = ImapMigrationService.ResolveHostAndPort(ep);

        Assert.Equal("imap.gmail.com", host);
        Assert.Equal(993, port);
    }

    [Fact]
    public void ResolveHostAndPort_RespectsCustomHostAndPort()
    {
        var ep = new ServerEndpoint
        {
            Protocol = ServerProtocol.Microsoft365,
            Host = "custom.m365.example.com",
            Port = 9993
        };

        var (host, port) = ImapMigrationService.ResolveHostAndPort(ep);

        Assert.Equal("custom.m365.example.com", host);
        Assert.Equal(9993, port);
    }

    [Fact]
    public void ServerEndpoint_IsOAuth2_InfersCorrectly()
    {
        var m365 = new ServerEndpoint { Protocol = ServerProtocol.Microsoft365 };
        Assert.True(m365.IsOAuth2);

        var google = new ServerEndpoint { Protocol = ServerProtocol.GoogleWorkspace };
        Assert.True(google.IsOAuth2);

        var imap = new ServerEndpoint { Protocol = ServerProtocol.Imap };
        Assert.False(imap.IsOAuth2);

        // Explicit override
        imap.IsOAuth2 = true;
        Assert.True(imap.IsOAuth2);
    }

    [Fact]
    public void OAuthTokenResult_PropertiesAndAlias()
    {
        var res = new OAuthTokenResult
        {
            Success = true,
            AccessToken = "mock_access_token_123",
            AccountEmail = "admin@example.com",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        Assert.True(res.Success);
        Assert.Equal("mock_access_token_123", res.AccessToken);
        Assert.Equal("admin@example.com", res.AccountUsername);
        Assert.Equal("admin@example.com", res.AccountEmail);
    }

    [Fact]
    public async Task PrepareAccountTokensAsync_StandardImap_NoTokenAssigned()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);

        var src = new ServerEndpoint { Protocol = ServerProtocol.Imap, Host = "mail.source.com", Port = 993 };
        var dst = new ServerEndpoint { Protocol = ServerProtocol.Imap, Host = "mail.dest.com", Port = 993 };

        var job = new AccountJob
        {
            SourceUser = "user1@source.com",
            SourcePassword = "password123",
            DestUser = "user1@dest.com",
            DestPassword = "password456"
        };

        bool ok = await orchestrator.PrepareAccountTokensAsync(src, dst, job);

        Assert.True(ok);
        Assert.True(string.IsNullOrEmpty(job.SourceOAuthToken));
        Assert.True(string.IsNullOrEmpty(job.DestOAuthToken));
    }

    [Fact]
    public async Task PrepareAccountTokensAsync_PassesThroughStaticToken()
    {
        var migrationService = new ImapMigrationService();
        var orchestrator = new BatchOrchestrator(migrationService);

        var src = new ServerEndpoint
        {
            Protocol = ServerProtocol.Microsoft365,
            OAuthAccessToken = "static_source_token"
        };
        var dst = new ServerEndpoint
        {
            Protocol = ServerProtocol.GoogleWorkspace,
            OAuthAccessToken = "static_dest_token"
        };

        var job = new AccountJob
        {
            SourceUser = "user1@source.com",
            DestUser = "user1@dest.com"
        };

        bool ok = await orchestrator.PrepareAccountTokensAsync(src, dst, job);

        Assert.True(ok);
        Assert.Equal("static_source_token", job.SourceOAuthToken);
        Assert.Equal("static_dest_token", job.DestOAuthToken);
    }

    [Fact]
    public async Task PrepareAccountTokensAsync_FailsGracefully_WhenServiceAccountJsonInvalid()
    {
        var migrationService = new ImapMigrationService();
        var oauthService = new OAuth2Service();
        var orchestrator = new BatchOrchestrator(migrationService, oauthService);

        var src = new ServerEndpoint
        {
            Protocol = ServerProtocol.GoogleWorkspace,
            GoogleServiceAccountJson = "{ \"invalid\": \"json\" }"
        };
        var dst = new ServerEndpoint { Protocol = ServerProtocol.Imap };

        var job = new AccountJob
        {
            SourceUser = "user1@google.com",
            DestUser = "user1@imap.com"
        };

        bool ok = await orchestrator.PrepareAccountTokensAsync(src, dst, job);

        Assert.False(ok);
        Assert.Equal(MigrationStatus.Failed, job.Status);
        Assert.Contains("Source Google Auth Error", job.StatusMessage);
    }
}
