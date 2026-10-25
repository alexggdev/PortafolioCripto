namespace CriptoPortfolio.Infrastructure.Alerts;

public sealed class PriceAlertOptions
{
    public const string SectionName = "PriceAlerts";

    /// <summary>Si es false, el worker no se registra (las alertas se pueden crear pero no se dispararían).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuántos segundos se revisan las alertas activas (solo lee memoria y una consulta SQL).</summary>
    public int IntervalSeconds { get; set; } = 30;

    public int LeaseSeconds { get; set; } = 90;

    public int StartupDelaySeconds { get; set; } = 20;

    public int MaxActivePerUser { get; set; } = 50;

    /// <summary>
    /// Si no hay precio en vivo reciente se usa el último de CoinGecko, pero solo si no tiene más de estos minutos:
    /// una alerta no debe dispararse con un precio viejo.
    /// </summary>
    public int MaxDelayedPriceMinutes { get; set; } = 30;

    /// <summary>Las alertas disparadas se borran solas pasados estos días.</summary>
    public int KeepTriggeredDays { get; set; } = 60;
}
