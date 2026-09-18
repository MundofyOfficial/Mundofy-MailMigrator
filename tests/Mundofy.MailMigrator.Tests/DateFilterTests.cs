using System.Globalization;
using Mundofy.MailMigrator.Core.Models;
using Xunit;

namespace Mundofy.MailMigrator.Tests;

public class DateFilterTests
{
    [Fact]
    public void IsDateAllowed_WithoutFilters_AllowsAllDates()
    {
        var options = new MigrationOptions();
        Assert.True(options.IsDateAllowed(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero)));
        Assert.True(options.IsDateAllowed(null));
    }

    [Fact]
    public void IsDateAllowed_WithSinceDate_FiltersOlderMessages()
    {
        var options = new MigrationOptions
        {
            SinceDate = new DateTime(2025, 6, 1)
        };

        var olderDate = new DateTimeOffset(2025, 5, 31, 23, 59, 0, TimeSpan.Zero);
        var exactDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var newerDate = new DateTimeOffset(2025, 6, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.False(options.IsDateAllowed(olderDate));
        Assert.True(options.IsDateAllowed(exactDate));
        Assert.True(options.IsDateAllowed(newerDate));
    }

    [Fact]
    public void IsDateAllowed_WithBeforeDate_FiltersNewerMessages()
    {
        var options = new MigrationOptions
        {
            BeforeDate = new DateTime(2025, 6, 1)
        };

        var olderDate = new DateTimeOffset(2025, 5, 31, 23, 59, 0, TimeSpan.Zero);
        var exactDate = new DateTimeOffset(2025, 6, 1, 23, 59, 59, TimeSpan.Zero);
        var newerDate = new DateTimeOffset(2025, 6, 2, 0, 0, 0, TimeSpan.Zero);

        Assert.True(options.IsDateAllowed(olderDate));
        Assert.True(options.IsDateAllowed(exactDate));
        Assert.False(options.IsDateAllowed(newerDate));
    }

    [Fact]
    public void IsDateAllowed_WithRange_FiltersOutsideWindow()
    {
        var options = new MigrationOptions
        {
            SinceDate = new DateTime(2025, 1, 1),
            BeforeDate = new DateTime(2025, 12, 31)
        };

        var tooEarly = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var inside = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var tooLate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(options.IsDateAllowed(tooEarly));
        Assert.True(options.IsDateAllowed(inside));
        Assert.False(options.IsDateAllowed(tooLate));
    }

    [Theory]
    [InlineData("18-09-2026", 2026, 9, 18)]
    [InlineData("01-01-2024", 2024, 1, 1)]
    [InlineData("31-12-2025", 2025, 12, 31)]
    public void EuropeanDateFormat_ParsesCorrectly(string input, int expectedYear, int expectedMonth, int expectedDay)
    {
        bool parsed = DateTime.TryParseExact(input, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt);
        Assert.True(parsed);
        Assert.Equal(expectedYear, dt.Year);
        Assert.Equal(expectedMonth, dt.Month);
        Assert.Equal(expectedDay, dt.Day);
    }
}
