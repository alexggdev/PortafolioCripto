using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.Entities;

/// <summary>Moneda del catálogo. Nunca se borra: se marca IsActive = false.</summary>
public class Asset
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? Rank { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Id de la moneda en CoinGecko (null si no existe allí).</summary>
    public string? CoinGeckoId { get; set; }

    /// <summary>Par de Binance contra USDT, p. ej. "BTCUSDT" (null si no cotiza).</summary>
    public string? BinanceSymbol { get; set; }

    /// <summary>Fuente única del histórico diario de esta moneda, para no mezclar cierres.</summary>
    public DataSource HistorySource { get; set; } = DataSource.CoinGecko;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public AssetPrice? Price { get; set; }
}
