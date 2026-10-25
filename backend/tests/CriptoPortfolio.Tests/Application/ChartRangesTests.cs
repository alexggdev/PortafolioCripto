using CriptoPortfolio.Application.Catalog;
using Xunit;

namespace CriptoPortfolio.Tests.Application;

public class ChartRangesTests
{
    [Theory]
    [InlineData("7D", ChartRange.Week)]
    [InlineData("7d", ChartRange.Week)]
    [InlineData("1m", ChartRange.Month)]
    [InlineData("3M", ChartRange.ThreeMonths)]
    [InlineData("1y", ChartRange.Year)]
    [InlineData("All", ChartRange.All)]
    [InlineData(" all ", ChartRange.All)]
    public void Parses_valid_ranges(string value, ChartRange expected)
    {
        Assert.True(ChartRanges.TryParse(value, out var range));
        Assert.Equal(expected, range);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1D")]
    [InlineData("abc")]
    [InlineData("Week")]
    public void Rejects_invalid_ranges(string? value)
    {
        Assert.False(ChartRanges.TryParse(value, out _));
    }

    [Fact]
    public void Days_and_codes_match()
    {
        Assert.Equal(7, ChartRanges.Days(ChartRange.Week));
        Assert.Equal(30, ChartRanges.Days(ChartRange.Month));
        Assert.Equal(90, ChartRanges.Days(ChartRange.ThreeMonths));
        Assert.Equal(365, ChartRanges.Days(ChartRange.Year));
        Assert.Null(ChartRanges.Days(ChartRange.All));
        Assert.Equal("1M", ChartRanges.Code(ChartRange.Month));
        Assert.Equal("All", ChartRanges.Code(ChartRange.All));
    }
}
