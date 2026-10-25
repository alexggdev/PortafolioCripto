using CriptoPortfolio.Application.Fx;

namespace CriptoPortfolio.Application.Abstractions;

/// <summary>
/// Últimos tipos de cambio válidos, en memoria. Los llena el worker; las peticiones de los usuarios
/// solo leen de aquí, nunca llaman al proveedor.
/// </summary>
public interface IFxRateCache
{
    FxSnapshot? Current { get; }

    void Set(FxSnapshot snapshot);
}
