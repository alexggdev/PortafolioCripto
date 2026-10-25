using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Fx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

/// <summary>Tipos de cambio para mostrar precios en otras monedas. Solo lee la caché en memoria.</summary>
[ApiController]
[Route("api/fx")]
[EnableRateLimiting(ApiServiceCollectionExtensions.PublicRateLimitPolicy)]
public class FxController : ControllerBase
{
    private readonly IFxRateCache _cache;

    public FxController(IFxRateCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Unidades de cada moneda por 1 USD. Si todavía no hay tipos de cambio (arranque o proveedor caído)
    /// responde solo con USD = 1 para que la web siga funcionando en dólares.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300)]
    [ProducesResponseType(typeof(FxRatesResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var snapshot = _cache.Current;
        if (snapshot is null)
        {
            return Ok(new FxRatesResponse(FxCurrencies.Base, new Dictionary<string, decimal> { [FxCurrencies.Base] = 1m }, null));
        }

        var rates = FxCurrencies.Supported
            .Where(snapshot.PerUsd.ContainsKey)
            .ToDictionary(c => c, c => snapshot.PerUsd[c]);

        return Ok(new FxRatesResponse(FxCurrencies.Base, rates, snapshot.ProviderUpdatedAtUtc ?? snapshot.FetchedAtUtc));
    }
}
