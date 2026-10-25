using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.Entities;

/// <summary>Movimiento de un portafolio. Todo en USD; la fecha es UTC.</summary>
public class Transaction
{
    public long Id { get; set; }
    public Guid PortfolioId { get; set; }
    public int AssetId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceUsd { get; set; }
    public decimal FeeUsd { get; set; }
    public DateTime DateUtc { get; set; }
    public string? Note { get; set; }
}
