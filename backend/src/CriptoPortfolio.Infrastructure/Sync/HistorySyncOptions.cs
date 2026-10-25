namespace CriptoPortfolio.Infrastructure.Sync;

public sealed class HistorySyncOptions
{
    public const string SectionName = "HistorySync";

    /// <summary>Si es false, el worker no se registra.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuántos minutos se mira si falta algún día. Si no falta nada, no hace ninguna llamada externa.</summary>
    public int IntervalMinutes { get; set; } = 10;

    public int LeaseMinutes { get; set; } = 30;

    /// <summary>Espera tras arrancar (deja terminar el catálogo y el mapa de Binance).</summary>
    public int StartupDelaySeconds { get; set; } = 45;

    /// <summary>Días que se piden en la carga inicial de una moneda de Binance.</summary>
    public int BinanceInitialDays { get; set; } = 1825;

    /// <summary>Días de la carga inicial con CoinGecko (el plan Demo llega a 365).</summary>
    public int CoinGeckoInitialDays { get; set; } = 365;

    /// <summary>Pausa entre llamadas, para no acercarnos al límite del proveedor.</summary>
    public int BinanceDelayMs { get; set; } = 250;

    public int CoinGeckoDelayMs { get; set; } = 2500;

    /// <summary>
    /// Para las monedas de CoinGecko, el cierre de ayer se toma del precio que el catálogo guarda en SQL
    /// (no gasta créditos de API) si ese precio es de los primeros minutos del día UTC. Pasada la ventana,
    /// el precio ya no sirve de cierre y se pide a la API.
    /// </summary>
    public int SnapshotWindowMinutes { get; set; } = 30;

    /// <summary>Tras estos fallos seguidos con un proveedor, se deja de llamarlo en esa pasada.</summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>Si una moneda no devuelve ningún día nuevo, no se vuelve a preguntar hasta pasadas estas horas.</summary>
    public int EmptyRetryHours { get; set; } = 6;

    /// <summary>Tras un fallo con una moneda, no se reintenta hasta pasados estos minutos.</summary>
    public int FailureRetryMinutes { get; set; } = 60;
}
