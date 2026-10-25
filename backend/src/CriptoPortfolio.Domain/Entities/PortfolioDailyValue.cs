namespace CriptoPortfolio.Domain.Entities;

/// <summary>Foto diaria del valor del portafolio. Clave: (PortfolioId, DateUtc).</summary>
public class PortfolioDailyValue
{
    public Guid PortfolioId { get; set; }
    public DateOnly DateUtc { get; set; }
    public decimal ValueUsd { get; set; }
    public decimal NetContributedUsd { get; set; }
}
