using System.Text.Json;
using System.Text.Json.Serialization;

namespace CriptoPortfolio.Infrastructure.Providers.CoinGecko;

/// <summary>Un elemento de GET /coins/markets. Solo los campos que usamos.</summary>
internal sealed class CoinGeckoMarketItem
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("symbol")] public string? Symbol { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
    [JsonPropertyName("market_cap_rank")] public int? MarketCapRank { get; set; }

    [JsonPropertyName("current_price")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? CurrentPrice { get; set; }

    [JsonPropertyName("market_cap")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("total_volume")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? TotalVolume { get; set; }

    [JsonPropertyName("price_change_percentage_1h_in_currency")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? Change1h { get; set; }

    [JsonPropertyName("price_change_percentage_24h_in_currency")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? Change24h { get; set; }

    [JsonPropertyName("price_change_percentage_7d_in_currency")]
    [JsonConverter(typeof(LenientNullableDecimalConverter))]
    public decimal? Change7d { get; set; }
}

/// <summary>
/// Lee un número como decimal? sin romper: null, números fuera de rango o valores raros dan null
/// en vez de una excepción que tiraría abajo la página entera de 250 monedas.
/// </summary>
internal sealed class LenientNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetDecimal(out var value) ? value : null;
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
