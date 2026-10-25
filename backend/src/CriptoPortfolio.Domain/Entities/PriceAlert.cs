using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.Entities;

/// <summary>
/// Alerta de precio de un usuario sobre una moneda. Se dispara una sola vez: al cumplirse se rellena TriggeredAtUtc
/// y deja de evaluarse. ReadAtUtc marca cuándo el usuario vio el aviso (la campana cuenta las no leídas).
/// </summary>
public class PriceAlert
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int AssetId { get; set; }
    public AlertDirection Direction { get; set; }
    public decimal TargetPriceUsd { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public DateTime? TriggeredAtUtc { get; set; }
    public decimal? TriggeredPriceUsd { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
