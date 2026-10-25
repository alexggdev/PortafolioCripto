namespace CriptoPortfolio.Infrastructure.Portfolios;

public enum CostBasisMethod
{
    Fifo,
    Average
}

public sealed class PortfolioOptions
{
    public const string SectionName = "Portfolio";

    /// <summary>Cómo se reparte el coste al vender: Fifo (por defecto) o Average (media ponderada).</summary>
    public CostBasisMethod CostBasisMethod { get; set; } = CostBasisMethod.Fifo;

    /// <summary>Tope de transacciones por usuario (evita abusos y mantiene rápido el cálculo).</summary>
    public int MaxTransactionsPerUser { get; set; } = 20_000;

    public string DefaultPortfolioName { get; set; } = "Mi portafolio";

    /// <summary>Tope de portafolios por usuario.</summary>
    public int MaxPortfoliosPerUser { get; set; } = 10;
}
