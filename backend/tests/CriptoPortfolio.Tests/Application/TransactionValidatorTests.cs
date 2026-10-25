using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.Enums;
using Xunit;

namespace CriptoPortfolio.Tests.Application;

public class TransactionValidatorTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static TransactionRequest Req(
        string? slug = "bitcoin",
        TransactionType type = TransactionType.Buy,
        decimal quantity = 1m,
        decimal? price = 100m,
        decimal? fee = 0m,
        DateTime? date = null,
        string? note = null) =>
        new(slug, type, quantity, price, fee, date ?? Now.AddDays(-1), note);

    private static TransactionValidation Check(TransactionRequest request) => TransactionValidator.Validate(request, Now);

    [Fact]
    public void A_normal_buy_is_valid_and_normalized()
    {
        var v = Check(Req(slug: "  Bitcoin ", note: "  primera compra  "));

        Assert.True(v.IsValid);
        Assert.Equal("bitcoin", v.Value!.AssetSlug);
        Assert.Equal("primera compra", v.Value.Note);
        Assert.Equal(DateTimeKind.Utc, v.Value.DateUtc.Kind);
    }

    [Fact]
    public void Blank_notes_become_null()
    {
        Assert.Null(Check(Req(note: "   ")).Value!.Note);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void The_asset_is_required(string? slug)
    {
        Assert.Contains("Elige una moneda.", Check(Req(slug: slug)).Errors);
    }

    [Fact]
    public void An_overlong_slug_is_rejected()
    {
        Assert.False(Check(Req(slug: new string('a', 201))).IsValid);
    }

    [Fact]
    public void An_unknown_type_is_rejected()
    {
        Assert.False(Check(Req(type: (TransactionType)99)).IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.1234567890123")]          // 13 decimales
    [InlineData("1000000000000000")]         // demasiado grande
    public void Invalid_quantities_are_rejected(string quantity)
    {
        Assert.False(Check(Req(quantity: decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture))).IsValid);
    }

    [Fact]
    public void Twelve_decimals_are_allowed_and_trailing_zeros_do_not_count()
    {
        Assert.True(Check(Req(quantity: 0.123456789012m)).IsValid);
        Assert.True(Check(Req(quantity: 1.500000000000000000m)).IsValid);
    }

    [Fact]
    public void Prices_and_fees_cannot_be_negative_or_too_precise()
    {
        Assert.False(Check(Req(price: -1m)).IsValid);
        Assert.False(Check(Req(fee: -0.01m)).IsValid);
        Assert.False(Check(Req(price: 0.1234567890123456789m)).IsValid);   // 19 decimales
        Assert.False(Check(Req(fee: 0.123456789m)).IsValid);               // 9 decimales
        Assert.False(Check(Req(price: 10_000_000_000m)).IsValid);
    }

    [Theory]
    [InlineData(TransactionType.Buy)]
    [InlineData(TransactionType.Sell)]
    public void Buys_and_sells_need_a_price(TransactionType type)
    {
        Assert.False(Check(Req(type: type, price: 0m)).IsValid);
        Assert.False(Check(Req(type: type, price: null)).IsValid);
    }

    [Theory]
    [InlineData(TransactionType.Staking)]
    [InlineData(TransactionType.Airdrop)]
    [InlineData(TransactionType.TransferOut)]
    [InlineData(TransactionType.Fee)]
    public void Other_types_do_not_need_a_price(TransactionType type)
    {
        var v = Check(Req(type: type, price: null, fee: null));

        Assert.True(v.IsValid);
        Assert.Equal(0m, v.Value!.UnitPriceUsd);
        Assert.Equal(0m, v.Value.FeeUsd);
    }

    [Fact]
    public void Dates_in_the_future_are_rejected_but_a_few_minutes_of_clock_skew_is_fine()
    {
        Assert.False(Check(Req(date: Now.AddHours(1))).IsValid);
        Assert.True(Check(Req(date: Now.AddMinutes(4))).IsValid);
    }

    [Fact]
    public void Dates_before_bitcoin_are_rejected()
    {
        Assert.False(Check(Req(date: new DateTime(2008, 12, 31, 0, 0, 0, DateTimeKind.Utc))).IsValid);
    }

    [Fact]
    public void Unspecified_dates_are_taken_as_utc()
    {
        var unspecified = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var v = Check(Req(date: unspecified));

        Assert.Equal(new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), v.Value!.DateUtc);
        Assert.Equal(DateTimeKind.Utc, v.Value.DateUtc.Kind);
    }

    [Fact]
    public void An_overlong_note_is_rejected()
    {
        Assert.False(Check(Req(note: new string('x', 501))).IsValid);
    }

    [Fact]
    public void Several_problems_are_reported_together()
    {
        var v = Check(Req(slug: "", quantity: 0m, fee: -1m));

        Assert.Equal(3, v.Errors.Count);
        Assert.Null(v.Value);
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("1.5", 1)]
    [InlineData("1.50", 1)]
    [InlineData("0.000000000001", 12)]
    [InlineData("100", 0)]
    public void ScaleOf_counts_significant_decimals(string value, int expected)
    {
        Assert.Equal(expected, TransactionValidator.ScaleOf(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
