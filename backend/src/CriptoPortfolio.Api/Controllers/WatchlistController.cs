using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Watchlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

/// <summary>Lista de seguimiento del usuario autenticado (la estrella de cada moneda).</summary>
[ApiController]
[Route("api/watchlist")]
[Authorize]
[EnableRateLimiting(ApiServiceCollectionExtensions.PrivateRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlist;

    public WatchlistController(IWatchlistService watchlist)
    {
        _watchlist = watchlist;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await _watchlist.ListAsync(userId, ct));
    }

    [HttpPut("{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(string slug, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _watchlist.AddAsync(userId, slug, ct);
        if (result.Succeeded)
        {
            return NoContent();
        }

        var status = result.Failure == WatchlistFailure.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails { Status = status, Title = "No se pudo seguir la moneda.", Detail = result.Error };
        problem.Extensions["errors"] = new[] { result.Error ?? "Error desconocido." };
        return StatusCode(status, problem);
    }

    [HttpDelete("{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(string slug, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await _watchlist.RemoveAsync(userId, slug, ct);
        return NoContent();
    }
}
