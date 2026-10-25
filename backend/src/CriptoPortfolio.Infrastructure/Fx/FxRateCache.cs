using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Fx;

namespace CriptoPortfolio.Infrastructure.Fx;

/// <summary>Singleton: una referencia que se sustituye entera, así que leerla es seguro sin candados.</summary>
public sealed class FxRateCache : IFxRateCache
{
    private volatile FxSnapshot? _current;

    public FxSnapshot? Current => _current;

    public void Set(FxSnapshot snapshot) => _current = snapshot;
}
