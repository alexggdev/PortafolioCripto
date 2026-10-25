namespace CriptoPortfolio.Application.Catalog;

/// <summary>Resultado de una pasada de sincronización del catálogo.</summary>
/// <param name="Executed">False si se omitió porque otra instancia tenía el candado.</param>
public sealed record CatalogSyncResult(
    bool Executed,
    int Processed,
    int Failed,
    int Created,
    int Deactivated,
    string? Error)
{
    public bool Succeeded => Executed && Error is null;

    public static CatalogSyncResult Skipped() => new(false, 0, 0, 0, 0, null);

    public static CatalogSyncResult Empty() => new(true, 0, 0, 0, 0, null);
}
