namespace CriptoPortfolio.Application.Catalog;

/// <summary>Una moneda tal como la entrega el proveedor del catálogo. Todo en USD.</summary>
public sealed record CatalogCoin(
    string CoinGeckoId,
    string Symbol,
    string Name,
    string? ImageUrl,
    int Rank,
    decimal? PriceUsd,
    decimal? MarketCapUsd,
    decimal? Volume24hUsd,
    decimal? Change1hPct,
    decimal? Change24hPct,
    decimal? Change7dPct);
