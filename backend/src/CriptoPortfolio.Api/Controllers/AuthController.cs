using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CriptoPortfolio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IRefreshTokenService _refresh;

    public AuthController(IAuthService auth, IRefreshTokenService refresh)
    {
        _auth = auth;
        _refresh = refresh;
    }

    [HttpPost("register")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        if (result.Succeeded)
        {
            return Ok(await StartSessionAsync(result.Response!, ct));
        }

        return Failure(result, "No se pudo crear la cuenta.");
    }

    [HttpPost("login")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (result.Succeeded)
        {
            return Ok(await StartSessionAsync(result.Response!, ct));
        }

        return Failure(result, "No se pudo iniciar sesión.");
    }

    /// <summary>Cambia un refresh token válido por un JWT nuevo y un refresh token nuevo (rotación).</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.PublicRateLimitPolicy)]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _refresh.RefreshAsync(request.RefreshToken, ct);
        if (result.Succeeded)
        {
            return Ok(result.Session);
        }

        return result.Failure == RefreshFailure.Concurrent
            ? Problem(StatusCodes.Status409Conflict, "La sesión se está renovando.", "La sesión se está renovando. Inténtalo de nuevo.")
            : Problem(StatusCodes.Status401Unauthorized, "No se pudo renovar la sesión.", "Tu sesión ha caducado. Vuelve a entrar.");
    }

    /// <summary>Cierra la sesión: el refresh token deja de valer. Siempre responde 204.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting(ApiServiceCollectionExtensions.PublicRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _refresh.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.GetUserId(),
            email = User.FindFirst("email")?.Value
        });
    }

    private async Task<SessionResponse> StartSessionAsync(AuthResponse auth, CancellationToken ct)
    {
        var refresh = await _refresh.IssueAsync(auth.UserId, ct);
        return SessionResponse.From(auth, refresh);
    }

    private IActionResult Problem(int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["errors"] = new[] { detail };
        return StatusCode(status, problem);
    }

    private IActionResult Failure(AuthResult result, string title)
    {
        var status = result.Failure switch
        {
            AuthFailure.InvalidCredentials => StatusCodes.Status401Unauthorized,
            AuthFailure.LockedOut => StatusCodes.Status429TooManyRequests,
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
