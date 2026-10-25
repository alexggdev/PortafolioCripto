using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

/// <summary>Alertas de precio del usuario autenticado. Los datos son privados: nunca se cachean.</summary>
[ApiController]
[Route("api/alerts")]
[Authorize]
[EnableRateLimiting(ApiServiceCollectionExtensions.PrivateRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AlertsController : ControllerBase
{
    private readonly IPriceAlertService _alerts;

    public AlertsController(IPriceAlertService alerts)
    {
        _alerts = alerts;
    }

    /// <summary>Todas las alertas del usuario: activas (con precio actual) y disparadas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PriceAlertDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await _alerts.ListAsync(userId, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PriceAlertDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _alerts.CreateAsync(userId, request, ct);
        if (result.Succeeded)
        {
            return StatusCode(StatusCodes.Status201Created, result.Alert);
        }

        var status = result.Failure == AlertFailure.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = "No se pudo crear la alerta.",
            Detail = string.Join(" ", result.Errors)
        };
        problem.Extensions["errors"] = result.Errors;
        return StatusCode(status, problem);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return await _alerts.DeleteAsync(userId, id, ct)
            ? NoContent()
            : Problem(title: "La alerta no existe.", statusCode: StatusCodes.Status404NotFound);
    }

    /// <summary>Cuántas alertas disparadas no ha visto el usuario (el número de la campana).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadAlertsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(new UnreadAlertsResponse(await _alerts.UnreadCountAsync(userId, ct)));
    }

    /// <summary>Marca como vistas todas las alertas disparadas.</summary>
    [HttpPost("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await _alerts.MarkAllReadAsync(userId, ct);
        return NoContent();
    }
}

public sealed record UnreadAlertsResponse(int Count);
