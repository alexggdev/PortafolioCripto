using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CriptoPortfolio.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    // Mismo mensaje para "no existe" y "contraseña mala": no se revela qué emails están registrados.
    private const string InvalidCredentialsMessage = "Credenciales incorrectas.";
    private const string LockedOutMessage = "Demasiados intentos fallidos. Inténtalo de nuevo en unos minutos.";

    private readonly UserManager<ApplicationUser> _users;
    private readonly ITokenService _tokens;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _log;

    public AuthService(
        UserManager<ApplicationUser> users,
        ITokenService tokens,
        TimeProvider time,
        ILogger<AuthService> log)
    {
        _users = users;
        _tokens = tokens;
        _time = time;
        _log = log;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // UserName = Email, así que un duplicado dispara dos errores: se resumen en uno.
            var duplicated = result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName");
            var errors = duplicated
                ? new[] { "Ese email ya está registrado." }
                : result.Errors.Select(e => e.Description).ToArray();

            _log.LogInformation("[API] Registro rechazado ({Codes})",
                string.Join(",", result.Errors.Select(e => e.Code)));
            return AuthResult.Fail(AuthFailure.ValidationFailed, errors);
        }

        _log.LogInformation("[API] Usuario registrado {UserId}", user.Id);
        return AuthResult.Ok(Issue(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials, InvalidCredentialsMessage);
        }

        if (await _users.IsLockedOutAsync(user))
        {
            _log.LogWarning("[API] Login bloqueado por intentos fallidos {UserId}", user.Id);
            return AuthResult.Fail(AuthFailure.LockedOut, LockedOutMessage);
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);

            if (await _users.IsLockedOutAsync(user))
            {
                _log.LogWarning("[API] Usuario bloqueado temporalmente {UserId}", user.Id);
                return AuthResult.Fail(AuthFailure.LockedOut, LockedOutMessage);
            }

            return AuthResult.Fail(AuthFailure.InvalidCredentials, InvalidCredentialsMessage);
        }

        await _users.ResetAccessFailedCountAsync(user);
        return AuthResult.Ok(Issue(user));
    }

    private AuthResponse Issue(ApplicationUser user)
    {
        var email = user.Email ?? "";
        var token = _tokens.CreateToken(user.Id, email);
        return new AuthResponse(token.Token, token.ExpiresAtUtc, user.Id, email);
    }
}
