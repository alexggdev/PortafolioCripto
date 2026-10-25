using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

/// <summary>Portafolio del usuario autenticado. Los datos son privados: nunca se cachean.</summary>
[ApiController]
[Route("api/portfolio")]
[Authorize]
[EnableRateLimiting(ApiServiceCollectionExtensions.PrivateRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolio;
    private readonly IPortfolioHistoryService _history;
    private readonly IPortfolioManager _manager;
    private readonly TimeProvider _time;

    public PortfolioController(
        IPortfolioService portfolio, IPortfolioHistoryService history, IPortfolioManager manager, TimeProvider time)
    {
        _time = time;
        _portfolio = portfolio;
        _history = history;
        _manager = manager;
    }

    // Un portafolio indicado que no es del usuario es "no existe": no se distingue de uno inexistente.
    private async Task<bool> OwnsAsync(Guid userId, Guid? portfolioId, CancellationToken ct) =>
        portfolioId is null || (await _manager.ListAsync(userId, ct)).Any(p => p.Id == portfolioId);

    private IActionResult PortfolioNotFound() => Problem(
        title: "El portafolio no existe.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>Posiciones valoradas al precio actual, con ganancia realizada y no realizada.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PortfolioSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] Guid? portfolioId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (!await OwnsAsync(userId, portfolioId, ct))
        {
            return PortfolioNotFound();
        }

        return Ok(await _portfolio.GetSummaryAsync(userId, portfolioId, ct));
    }

    /// <summary>Todos los portafolios del usuario sumados: totales, valor de cada uno y monedas juntas.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(PortfolioOverview), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await _portfolio.GetOverviewAsync(userId, ct));
    }

    /// <summary>
    /// Gráfica del portafolio: valor y dinero aportado por día. range = 7D, 1M, 3M, 1Y o All (por defecto 1M).
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PortfolioHistory), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> History([FromQuery] string? range, [FromQuery] Guid? portfolioId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var parsed = ChartRange.Month;
        if (!string.IsNullOrWhiteSpace(range) && !ChartRanges.TryParse(range, out parsed))
        {
            return Problem(
                title: "Rango no válido.",
                detail: "Usa 7D, 1M, 3M, 1Y o All.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await OwnsAsync(userId, portfolioId, ct))
        {
            return PortfolioNotFound();
        }

        return Ok(await _history.GetHistoryAsync(userId, parsed, portfolioId, ct));
    }

    /// <summary>Transacciones, las más recientes primero. assetSlug filtra por moneda y type por tipo.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? assetSlug,
        [FromQuery] Guid? portfolioId,
        [FromQuery] TransactionType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (!await OwnsAsync(userId, portfolioId, ct))
        {
            return PortfolioNotFound();
        }

        return Ok(await _portfolio.ListTransactionsAsync(userId, new TransactionListQuery(assetSlug, page, pageSize, portfolioId, type), ct));
    }

    /// <summary>
    /// Descarga las transacciones de un portafolio en CSV. format = standard (coma, punto decimal) o excel
    /// (punto y coma, coma decimal, BOM). Por defecto standard.
    /// </summary>
    [HttpGet("transactions/export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? portfolioId, [FromQuery] string? format, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        CsvFormat csvFormat;
        switch (format?.Trim().ToLowerInvariant())
        {
            case null or "" or "standard":
                csvFormat = CsvFormat.Standard;
                break;
            case "excel":
                csvFormat = CsvFormat.Excel;
                break;
            default:
                return Problem(
                    title: "Formato no válido.",
                    detail: "Usa standard o excel.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var portfolios = await _manager.ListAsync(userId, ct);
        var selected = portfolioId is null
            ? portfolios.OrderBy(p => p.CreatedAtUtc).FirstOrDefault()
            : portfolios.FirstOrDefault(p => p.Id == portfolioId);
        if (portfolioId is not null && selected is null)
        {
            return PortfolioNotFound();
        }

        var transactions = await _portfolio.ExportTransactionsAsync(userId, selected?.Id, ct);
        var bytes = TransactionCsv.ToBytes(transactions, csvFormat);
        var fileName = TransactionCsv.FileName(selected?.Name, _time.GetUtcNow().UtcDateTime);

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpPost("transactions")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add([FromBody] TransactionRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _portfolio.AddTransactionAsync(userId, request, ct);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Transaction) : Failure(result, "No se pudo guardar la transacción.");
    }

    [HttpPut("transactions/{id:long}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long id, [FromBody] TransactionRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _portfolio.UpdateTransactionAsync(userId, id, request, ct);
        return result.Succeeded ? Ok(result.Transaction) : Failure(result, "No se pudo guardar la transacción.");
    }

    [HttpDelete("transactions/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _portfolio.DeleteTransactionAsync(userId, id, ct);
        return result.Succeeded ? NoContent() : Failure(result, "No se pudo borrar la transacción.");
    }

    /// <summary>Quita una moneda del portafolio indicado (o del primero): borra todas sus transacciones.</summary>
    [HttpDelete("assets/{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAsset(string slug, [FromQuery] Guid? portfolioId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _portfolio.RemoveAssetAsync(userId, slug, portfolioId, ct);
        return result.Succeeded ? NoContent() : Failure(result, "No se pudo quitar la moneda.");
    }

    /// <summary>Mueve una moneda, con todo su historial, a otro portafolio del usuario.</summary>
    [HttpPost("assets/{slug}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveAsset(string slug, [FromBody] MoveAssetRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _portfolio.MoveAssetAsync(userId, slug, request.FromPortfolioId, request.ToPortfolioId, ct);
        return result.Succeeded ? NoContent() : Failure(result, "No se pudo mover la moneda.");
    }

    private IActionResult Failure(TransactionResult result, string title)
    {
        var status = result.Failure switch
        {
            TransactionFailure.AssetNotFound or TransactionFailure.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = string.Join(" ", result.Errors)
        };
        problem.Extensions["errors"] = result.Errors;

        return StatusCode(status, problem);
    }
}
