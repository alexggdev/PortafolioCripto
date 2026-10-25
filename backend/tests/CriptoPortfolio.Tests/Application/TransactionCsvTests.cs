using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.Enums;
using Xunit;

namespace CriptoPortfolio.Tests.Application;

public class TransactionCsvTests
{
    private static TransactionDto Tx(
        TransactionType type = TransactionType.Buy,
        decimal quantity = 0.5m,
        decimal price = 60000m,
        decimal fee = 1.25m,
        string name = "Bitcoin",
        string? note = null) =>
        new(1, "bitcoin", "btc", name, null, type, quantity, price, fee, new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), note);

    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Standard_uses_comma_dot_and_iso_utc_dates()
    {
        var lines = Lines(TransactionCsv.ToText(new[] { Tx() }, CsvFormat.Standard));

        Assert.Equal("Fecha (UTC),Tipo,Moneda,Símbolo,Cantidad,Precio unitario (USD),Comisión (USD),Nota", lines[0]);
        Assert.Equal("2026-03-04T05:06:07Z,Buy,Bitcoin,BTC,0.5,60000,1.25,", lines[1]);
    }

    [Fact]
    public void Excel_uses_semicolon_and_decimal_comma_with_bom()
    {
        var bytes = TransactionCsv.ToBytes(new[] { Tx() }, CsvFormat.Excel);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        var lines = Lines(System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Equal("2026-03-04T05:06:07Z;Buy;Bitcoin;BTC;0,5;60000;1,25;", lines[1]);
    }

    [Fact]
    public void Standard_has_no_bom()
    {
        var bytes = TransactionCsv.ToBytes(new[] { Tx() }, CsvFormat.Standard);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public void Decimals_are_never_in_scientific_notation_and_keep_precision()
    {
        var line = Lines(TransactionCsv.ToText(
            new[] { Tx(quantity: 0.000000000001m, price: 0.000000000000000001m, fee: 0m) }, CsvFormat.Standard))[1];

        Assert.Contains(",0.000000000001,0.000000000000000001,0,", line);
    }

    [Fact]
    public void Fields_with_separator_quotes_or_newlines_are_quoted()
    {
        var line = Lines(TransactionCsv.ToText(new[] { Tx(note: "compra \"grande\", en Binance") }, CsvFormat.Standard))[1];
        Assert.EndsWith(",\"compra \"\"grande\"\", en Binance\"", line);

        var multi = TransactionCsv.ToText(new[] { Tx(note: "uno\ndos") }, CsvFormat.Standard);
        Assert.Contains("\"uno\ndos\"", multi);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+34 600", "'+34 600")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("normal", "normal")]
    public void Text_cells_that_look_like_formulas_are_neutralized(string note, string expected)
    {
        var line = Lines(TransactionCsv.ToText(new[] { Tx(note: note) }, CsvFormat.Standard))[1];
        Assert.EndsWith("," + expected, line);
    }

    [Fact]
    public void Negative_numbers_are_not_touched_only_text_is_protected()
    {
        var line = Lines(TransactionCsv.ToText(new[] { Tx(name: "=HYPERLINK(\"x\")") }, CsvFormat.Standard))[1];
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", line);
    }

    [Fact]
    public void Empty_list_still_has_the_header()
    {
        Assert.Single(Lines(TransactionCsv.ToText(Array.Empty<TransactionDto>(), CsvFormat.Standard)));
    }

    [Theory]
    [InlineData("Largo plazo", "transacciones-largo-plazo-20261017.csv")]
    [InlineData("Ahorro ñandú / Año 2", "transacciones-ahorro-nandu-ano-2-20261017.csv")]
    [InlineData("../../etc", "transacciones-etc-20261017.csv")]
    [InlineData("???", "transacciones-portafolio-20261017.csv")]
    [InlineData(null, "transacciones-portafolio-20261017.csv")]
    public void File_name_is_ascii_safe(string? name, string expected)
    {
        Assert.Equal(expected, TransactionCsv.FileName(name, new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc)));
    }
}
