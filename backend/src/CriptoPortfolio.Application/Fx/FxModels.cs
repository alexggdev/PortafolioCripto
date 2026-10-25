namespace CriptoPortfolio.Application.Fx;

/// <summary>Monedas en las que se puede mostrar la web. Todo se guarda y calcula en USD; esto es solo presentación.</summary>
public static class FxCurrencies
{
    public const string Base = "USD";

    public static readonly IReadOnlyList<string> Supported = new[] { "USD", "EUR", "PEN", "CLP" };

    /// <summary>Monedas que hay que pedir al proveedor (todas menos la base).</summary>
    public static IEnumerable<string> Quoted => Supported.Where(c => c != Base);
}

/// <summary>Unidades de cada moneda por 1 USD. Siempre incluye USD = 1.</summary>
public sealed record FxSnapshot(
    IReadOnlyDictionary<string, decimal> PerUsd,
    DateTime FetchedAtUtc,
    DateTime? ProviderUpdatedAtUtc);

/// <summary>Lo que devuelve el proveedor antes de validarlo.</summary>
public sealed record FxFetch(IReadOnlyDictionary<string, decimal> PerUsd, DateTime? ProviderUpdatedAtUtc);

public sealed record FxRefreshResult(bool Updated, string? Error);

/// <summary>Respuesta pública de GET /api/fx.</summary>
public sealed record FxRatesResponse(string Base, IReadOnlyDictionary<string, decimal> Rates, DateTime? UpdatedAtUtc);
