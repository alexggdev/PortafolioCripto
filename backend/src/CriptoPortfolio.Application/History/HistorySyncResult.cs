namespace CriptoPortfolio.Application.History;

/// <summary>Resultado de una pasada del histórico diario.</summary>
/// <param name="Executed">False si se omitió porque otra instancia tenía el candado.</param>
/// <param name="Processed">Monedas que tenían trabajo y se completaron.</param>
/// <param name="Inserted">Días nuevos guardados.</param>
/// <param name="Failed">Monedas que fallaron (se reintentan en la siguiente pasada).</param>
/// <param name="Pending">Monedas con trabajo que quedaron para otra pasada (proveedor detenido o esperando el cierre del día).</param>
public sealed record HistorySyncResult(bool Executed, int Processed, int Inserted, int Failed, int Pending, string? Error)
{
    public bool Succeeded => Executed && Error is null;

    public static HistorySyncResult Skipped() => new(false, 0, 0, 0, 0, null);
}
