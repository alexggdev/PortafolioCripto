using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Portfolios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

public sealed record PortfolioNameRequest(string? Name);

public sealed record PortfolioRequest(string? Name, int? Avatar, bool? IncludeInTotal, bool? IsDefault);

public sealed record PortfolioOrderRequest(Guid[]? Ids);

/// <summary>Los portafolios del usuario autenticado: listar, crear, cambiar, duplicar, ordenar y borrar.</summary>
[ApiController]
[Route("api/portfolios")]
[Authorize]
[EnableRateLimiting(ApiServiceCollectionExtensions.PrivateRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PortfoliosController : ControllerBase
{
    private readonly IPortfolioManager _manager;

    public PortfoliosController(IPortfolioManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PortfolioInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await _manager.ListAsync(userId, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PortfolioInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] PortfolioRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _manager.CreateAsync(userId, request.Name, request.Avatar, ct);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Portfolio)
            : Failure(result, "No se pudo crear el portafolio.");
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PortfolioInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PortfolioRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var changes = new PortfolioChanges(request.Name, request.Avatar, request.IncludeInTotal, request.IsDefault);
        var result = await _manager.UpdateAsync(userId, id, changes, ct);
        return result.Succeeded ? Ok(result.Portfolio) : Failure(result, "No se pudo guardar el portafolio.");
    }

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(PortfolioInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] PortfolioNameRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _manager.DuplicateAsync(userId, id, request.Name, ct);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Portfolio)
            : Failure(result, "No se pudo duplicar el portafolio.");
    }

    [HttpPut("order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder([FromBody] PortfolioOrderRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _manager.ReorderAsync(userId, request.Ids ?? Array.Empty<Guid>(), ct);
        return result.Succeeded ? NoContent() : Failure(result, "No se pudo cambiar el orden.");
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _manager.DeleteAsync(userId, id, ct);
        return result.Succeeded ? NoContent() : Failure(result, "No se pudo borrar el portafolio.");
    }

    private IActionResult Failure(PortfolioResult result, string title)
    {
        var status = result.Failure == PortfolioFailure.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails { Status = status, Title = title, Detail = string.Join(" ", result.Errors) };
        problem.Extensions["errors"] = result.Errors;
        return StatusCode(status, problem);
    }
}
