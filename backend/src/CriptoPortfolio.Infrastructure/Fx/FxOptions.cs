namespace CriptoPortfolio.Infrastructure.Fx;

public sealed class FxOptions
{
    public const string SectionName = "FxRates";

    /// <summary>Si es false, el worker no se registra y la web solo ofrece USD.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuántos minutos se piden tipos de cambio nuevos (el proveedor solo actualiza una vez al día).</summary>
    public int RefreshMinutes { get; set; } = 360;

    /// <summary>Espera tras un fallo antes de reintentar.</summary>
    public int RetryMinutes { get; set; } = 10;

    public int StartupDelaySeconds { get; set; } = 3;

    /// <summary>
    /// Un tipo nuevo no puede diferir más de esta fracción (0,5 = 50 %) del anterior; si lo hace se descarta
    /// la respuesta entera por sospechosa. No se aplica si el anterior tiene más de <see cref="IgnoreCheckAfterDays"/> días.
    /// </summary>
    public decimal MaxChangeFraction { get; set; } = 0.5m;

    public int IgnoreCheckAfterDays { get; set; } = 7;
}
