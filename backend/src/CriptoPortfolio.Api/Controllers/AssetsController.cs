using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

/// <summary>Catálogo público (sin login). Lee de SQL y de la caché en vivo; nunca llama a proveedores externos.</summary>
[ApiController]
[Route("api/assets")]
[EnableRateLimiting(ApiServiceCollectionExtensions.PublicRateLimitPolicy)]
public class AssetsController : ControllerBase
{
    private const int MaxSlugLength = 200;

    private readonly IAssetQueryService _assets;
    private readonly IAssetChartService _charts;

    public AssetsController(IAssetQueryService assets, IAssetChartService charts)
    {
        _assets = assets;
        _charts = charts;
    }

    /// <summary>Lista paginada. sort: Rank, Name, Price, Change24h, MarketCap, Volume.</summary>
    [HttpGet]
    [ResponseCache(Duration = 5)]
    [ProducesResponseType(typeof(PagedResult<AssetSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] AssetSort sort = AssetSort.Rank,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _assets.ListAsync(new AssetListQuery(search, sort, desc, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    [ResponseCache(Duration = 5)]
    [ProducesResponseType(typeof(AssetSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        if (slug.Length > MaxSlugLength)
        {
            return NotFound();
        }

        var asset = await _assets.GetBySlugAsync(slug, ct);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>Serie para el gráfico. range: 7D, 1M (por defecto), 3M, 1Y o All.</summary>
    [HttpGet("{slug}/chart")]
    [ResponseCache(Duration = 30)]
    [ProducesResponseType(typeof(AssetChart), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chart(string slug, [FromQuery] string range = "1M", CancellationToken ct = default)
    {
        if (!ChartRanges.TryParse(range, out var parsed))
        {
            return Problem(
                title: "Rango no válido.",
                detail: "Usa 7D, 1M, 3M, 1Y o All.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (slug.Length > MaxSlugLength)
        {
            return NotFound();
        }

        var chart = await _charts.GetChartAsync(slug, parsed, ct);
        return chart is null ? NotFound() : Ok(chart);
    }
}
