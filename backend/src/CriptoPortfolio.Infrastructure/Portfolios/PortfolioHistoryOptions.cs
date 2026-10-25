namespace CriptoPortfolio.Infrastructure.Portfolios;

public sealed class PortfolioHistoryOptions
{
    public const string SectionName = "PortfolioHistory";

    /// <summary>Si es false, el worker de fotos diarias no se registra (la gráfica sigue funcionando: se completa al pedirla).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuántos minutos el worker mira si falta alguna foto. Si no falta nada, solo hace una consulta por portafolio.</summary>
    public int IntervalMinutes { get; set; } = 30;

    public int LeaseMinutes { get; set; } = 20;

    public int StartupDelaySeconds { get; set; } = 90;

    /// <summary>
    /// Un día solo se guarda cuando todas las monedas con histórico ya tienen su cierre (así no se guarda un valor
    /// provisional). Si una moneda lleva más de estos días sin cierre nuevo, se la ignora para no bloquear al resto.
    /// </summary>
    public int StalePriceDays { get; set; } = 3;

    /// <summary>Tope de puntos por respuesta; "Todo" con años de historia se reduce a este número.</summary>
    public int MaxPoints { get; set; } = 500;
}
