using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Abstractions;

public interface ICatalogSyncService
{
    /// <summary>
    /// Una pasada: toma el candado, pide el catálogo al proveedor, actualiza SQL y suelta el candado.
    /// Los fallos del proveedor no lanzan excepción: quedan en el resultado y en SyncStates.
    /// </summary>
    Task<CatalogSyncResult> SyncAsync(string owner, CancellationToken ct = default);
}
