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
    public void TestImapCopyCfgParser_WithOriginalDistCfg()
    {
        string distPath = Path.Combine("..", "..", "..", "..", "..", "Dist", "ImapCopy.cfg");
        if (File.Exists(distPath))
        {
            var result = ImapCopyCfgParser.ParseFile(distPath);
            Assert.Equal("localhost", result.SourceEndpoint.Host);
            Assert.Equal(143, result.SourceEndpoint.Port);
            Assert.Equal("localhost", result.DestEndpoint.Host);
            Assert.Equal(143, result.DestEndpoint.Port);
            Assert.Contains("\\Recent", result.Options.DenyFlags);
            Assert.Equal(2, result.Accounts.Count);
            Assert.Equal("foo", result.Accounts[0].SourceUser);
            Assert.Equal("bar", result.Accounts[1].SourceUser);
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
}
