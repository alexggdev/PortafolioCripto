namespace CriptoPortfolio.Application.Prices;

/// <summary>Último precio en vivo de una moneda (USD) y cuándo se leyó.</summary>
public sealed record LivePrice(decimal PriceUsd, DateTime UpdatedAtUtc);
