using Mundofy.MailMigrator.Core.Models;
using Mundofy.MailMigrator.Core.Parsers;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class ParserTests
{
    [Fact]
    public void TestImapCopyCfgParser_WithSampleConfig()
    {
        string sampleConfig = @"
# ImapCopy Config Sample
SourceProtocol IMAP
SourceServer mail.source.com
SourcePort 993

DestServer mail.dest.com
DestPort 993

skipfolder INBOX.Trash
skipfolder INBOX.Spam
skipmatch ""INBOX.Temp.""
copyfolder INBOX
DstRootFolder ""OldArchive""
DenyFlags ""\Recent""
Concurrency 6

Copy ""user1"" ""pass1"" ""user1_dest"" ""pass1_dest""
Copy ""user2"" ""pass2"" * *
";

        var result = ImapCopyCfgParser.Parse(sampleConfig);

        Assert.Equal(ServerProtocol.Imap, result.SourceEndpoint.Protocol);
        Assert.Equal("mail.source.com", result.SourceEndpoint.Host);
        Assert.Equal(993, result.SourceEndpoint.Port);
        Assert.True(result.SourceEndpoint.UseSsl);

        Assert.Equal("mail.dest.com", result.DestEndpoint.Host);
        Assert.Equal(993, result.DestEndpoint.Port);

        Assert.Equal(6, result.Options.MaxConcurrency);
        Assert.Contains("INBOX.Trash", result.Options.SkipFolders);
        Assert.Contains("INBOX.Spam", result.Options.SkipFolders);
        Assert.Contains("INBOX.Temp.", result.Options.SkipPrefixes);
        Assert.Contains("INBOX", result.Options.CopyFolders);
        Assert.Equal("OldArchive", result.Options.DstRootFolder);
        Assert.Contains("\\Recent", result.Options.DenyFlags);

        Assert.Equal(2, result.Accounts.Count);
        Assert.Equal("user1", result.Accounts[0].SourceUser);
        Assert.Equal("pass1", result.Accounts[0].SourcePassword);
        Assert.Equal("user1_dest", result.Accounts[0].DestUser);
        Assert.Equal("pass1_dest", result.Accounts[0].DestPassword);

        // Wildcard mapping * *
        Assert.Equal("user2", result.Accounts[1].SourceUser);
        Assert.Equal("user2", result.Accounts[1].DestUser);
        Assert.Equal("pass2", result.Accounts[1].DestPassword);
    }

    [Fact]
    public void TestImapCopyCfgParser_PureLegacyOriginalFormat()
    {
        // Authentic 2001-2009 Armin Diehl imapcopy format with zero new flags
        string legacyConfig = @"
; Original ImapCopy Config
SourceServer oldmail.domain.com
SourcePort 143
DestServer newmail.domain.com
DestPort 143

DebugSrc
DebugDst
DenyFlags \Recent
converttimezone UTC +0000

Copy foo foopass foo foodestpass
Copy bar barpass * *
";
        var result = ImapCopyCfgParser.Parse(legacyConfig);

        Assert.Equal("oldmail.domain.com", result.SourceEndpoint.Host);
        Assert.Equal(143, result.SourceEndpoint.Port);
        Assert.False(result.SourceEndpoint.UseSsl); // Port 143 defaults to STARTTLS / non-SSL
        Assert.Equal("newmail.domain.com", result.DestEndpoint.Host);
        Assert.Equal(143, result.DestEndpoint.Port);
        Assert.False(result.DestEndpoint.UseSsl);
        Assert.False(result.SourceEndpoint.AllowInvalidCertificates);
        Assert.Null(result.LogFilePath);
        Assert.Equal(2, result.Accounts.Count);

        Assert.Equal("foo", result.Accounts[0].SourceUser);
        Assert.Equal("foopass", result.Accounts[0].SourcePassword);
        Assert.Equal("foo", result.Accounts[0].DestUser);
        Assert.Equal("foodestpass", result.Accounts[0].DestPassword);

        // Wildcards handled identically
        Assert.Equal("bar", result.Accounts[1].SourceUser);
        Assert.Equal("barpass", result.Accounts[1].SourcePassword);
        Assert.Equal("bar", result.Accounts[1].DestUser);
        Assert.Equal("barpass", result.Accounts[1].DestPassword);
    }

    [Fact]
    public void TestImapCopyCfgParser_WithOriginalDistCfg()
    {
        string distPath = Path.Combine("..", "..", "..", "..", "..", "Dist", "ImapCopy.cfg");
        if (File.Exists(distPath))
        {
            var result = ImapCopyCfgParser.ParseFile(distPath);
            Assert.Equal("mail.oldschool.com", result.SourceEndpoint.Host);
            Assert.Equal(993, result.SourceEndpoint.Port);
            Assert.Equal("mail.newschool.com", result.DestEndpoint.Host);
            Assert.Equal(993, result.DestEndpoint.Port);
            Assert.Equal(4, result.Options.MaxConcurrency);
            Assert.Equal("migration.txt", result.LogFilePath);
            Assert.Contains("\\Recent", result.Options.DenyFlags);
            Assert.Equal(3, result.Accounts.Count);
            Assert.Equal("alice@oldschool.com", result.Accounts[0].SourceUser);
            Assert.Equal("bob@oldschool.com", result.Accounts[1].SourceUser);
            Assert.Equal("sales@oldschool.com", result.Accounts[2].SourceUser);
        }
    }

    [Fact]
    public void TestCsvAccountParser_CommaAndHeaders()
    {
        string csv = @"SourceUser,SourcePass,DestUser,DestPass
alice@example.com,pass123,alice@newcorp.com,newpass123
bob@example.com,secret,*,*";

        var accounts = CsvAccountParser.Parse(csv);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("alice@example.com", accounts[0].SourceUser);
        Assert.Equal("alice@newcorp.com", accounts[0].DestUser);
        Assert.Equal("bob@example.com", accounts[1].SourceUser);
        Assert.Equal("bob@example.com", accounts[1].DestUser);
        Assert.Equal("secret", accounts[1].DestPassword);
    }

    [Fact]
    public void TestCsvAccountParser_TabDelimited()
    {
        string tsv = "charlie\tpw1\tcharlie_new\tpw2\r\ndavid\tpw3\tdavid_new\tpw4";

        var accounts = CsvAccountParser.Parse(tsv);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("charlie", accounts[0].SourceUser);
        Assert.Equal("pw1", accounts[0].SourcePassword);
        Assert.Equal("charlie_new", accounts[0].DestUser);
    }

    [Fact]
    public void TestCsvAccountParser_TwoColumnEmailMapping()
    {
        string csv = @"john@source.com,john@dest.com
sarah@source.com,sarah@dest.com";

        var accounts = CsvAccountParser.Parse(csv);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("john@source.com", accounts[0].SourceUser);
        Assert.Equal("john@dest.com", accounts[0].DestUser);
        Assert.Empty(accounts[0].SourcePassword);
        Assert.Empty(accounts[0].DestPassword);
        Assert.Equal("sarah@source.com", accounts[1].SourceUser);
        Assert.Equal("sarah@dest.com", accounts[1].DestUser);
    }

    [Fact]
    public void TestCsvAccountParser_SingleColumnMailboxes()
    {
        string csv = "user1@tenant.onmicrosoft.com\r\nuser2@tenant.onmicrosoft.com";

        var accounts = CsvAccountParser.Parse(csv);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("user1@tenant.onmicrosoft.com", accounts[0].SourceUser);
        Assert.Equal("user1@tenant.onmicrosoft.com", accounts[0].DestUser);
        Assert.Empty(accounts[0].SourcePassword);
    }

    [Fact]
    public void TestCsvAccountParser_HeaderTwoColumnSourceDest()
    {
        string csv = @"SourceEmail,DestEmail
ceo@oldcorp.com,ceo@newcorp.com";

        var accounts = CsvAccountParser.Parse(csv);

        Assert.Single(accounts);
        Assert.Equal("ceo@oldcorp.com", accounts[0].SourceUser);
        Assert.Equal("ceo@newcorp.com", accounts[0].DestUser);
        Assert.Empty(accounts[0].SourcePassword);
    }

    [Fact]
    public void TestCsvAccountParser_M365ToImap_ThreeColumns()
    {
        string csv = "user@m365.com,user@nextcloud.local,SecretPass123";

        var accounts = CsvAccountParser.Parse(csv, ServerProtocol.Microsoft365, ServerProtocol.Imap);

        Assert.Single(accounts);
        Assert.Equal("user@m365.com", accounts[0].SourceUser);
        Assert.Empty(accounts[0].SourcePassword);
        Assert.Equal("user@nextcloud.local", accounts[0].DestUser);
        Assert.Equal("SecretPass123", accounts[0].DestPassword);
    }

    [Fact]
    public void TestMigrationOptions_FolderFiltering()
    {
        var options = new MigrationOptions();
        options.SkipFolders.Add("Trash");
        options.SkipPrefixes.Add("Junk");

        Assert.True(options.ShouldCopyFolder("INBOX"));
        Assert.True(options.ShouldCopyFolder("Sent"));
        Assert.False(options.ShouldCopyFolder("Trash"));
        Assert.False(options.ShouldCopyFolder("Junk/Old"));
    }

    [Theory]
    [InlineData("AllowInvalidCertificates Yes", true)]
    [InlineData("AllowInvalidCerts true", true)]
    [InlineData("PermitUnsignedSSL 1", true)]
    [InlineData("InsecureSSL No", false)]
    [InlineData("", false)]
    public void TestImapCopyCfgParser_AllowInvalidCertificates(string directive, bool expected)
    {
        string cfg = $@"
SourceServer mail.test.com
SourcePort 993
DestServer mail.dest.com
DestPort 993
{directive}
Copy ""u1"" ""p1"" ""u2"" ""p2""
";
        var result = ImapCopyCfgParser.Parse(cfg);
        Assert.Equal(expected, result.SourceEndpoint.AllowInvalidCertificates);
        Assert.Equal(expected, result.DestEndpoint.AllowInvalidCertificates);
    }

    [Theory]
    [InlineData("LogFile \"C:\\Logs\\migration.txt\"", "C:\\Logs\\migration.txt")]
    [InlineData("Log migration.log", "migration.log")]
    [InlineData("ExportLog out.txt", "out.txt")]
    public void TestImapCopyCfgParser_LogFile(string directive, string expected)
    {
        string cfg = $@"
SourceServer mail.test.com
DestServer mail.dest.com
{directive}
Copy ""u1"" ""p1"" ""u2"" ""p2""
";
        var result = ImapCopyCfgParser.Parse(cfg);
        Assert.Equal(expected, result.LogFilePath);
    }
}
