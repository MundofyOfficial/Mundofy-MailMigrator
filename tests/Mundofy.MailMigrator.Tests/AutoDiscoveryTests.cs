using System.Threading.Tasks;
using Mundofy.MailMigrator.Core.Services;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class AutoDiscoveryTests
{
    private readonly ServerAutoDiscoveryService _service = new();

    [Theory]
    [InlineData("user@gmail.com", "imap.gmail.com", 993, true)]
    [InlineData("test@googlemail.com", "imap.gmail.com", 993, true)]
    [InlineData("admin@outlook.com", "outlook.office365.com", 993, true)]
    [InlineData("office@office365.com", "outlook.office365.com", 993, true)]
    [InlineData("person@yahoo.com", "imap.mail.yahoo.com", 993, true)]
    [InlineData("contact@icloud.com", "imap.mail.me.com", 993, true)]
    [InlineData("info@fastmail.com", "imap.fastmail.com", 993, true)]
    public async Task TestWellKnownProviders(string email, string expectedHost, int expectedPort, bool expectedSsl)
    {
        var result = await _service.DiscoverAsync(email);

        Assert.True(result.Success);
        Assert.Equal(expectedHost, result.Host);
        Assert.Equal(expectedPort, result.Port);
        Assert.Equal(expectedSsl, result.UseSsl);
    }

    [Fact]
    public async Task TestInvalidInput_FailsGracefully()
    {
        var empty = await _service.DiscoverAsync("");
        Assert.False(empty.Success);

        var invalid = await _service.DiscoverAsync("notanemail");
        Assert.False(invalid.Success);
    }

    [Fact]
    public async Task TestDomainFallback_ReturnsImapPrefix()
    {
        var result = await _service.DiscoverAsync("user@example-nonexistent-domain-12345.com");
        Assert.True(result.Success);
        Assert.Equal("imap.example-nonexistent-domain-12345.com", result.Host);
        Assert.Equal(993, result.Port);
        Assert.True(result.UseSsl);
    }
}
