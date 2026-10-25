using System.Security.Cryptography;
using System.Text;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Auth;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Jwt;
using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Identity;

public sealed class RefreshTokenService : IRefreshTokenService
{
    // Si llega dos veces el mismo token con segundos de diferencia es casi siempre la web lanzando dos peticiones
    // a la vez, no un robo. Pasado este margen, reutilizar un token ya rotado sí se trata como robo.
    private static readonly TimeSpan ConcurrencyGrace = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeepRevoked = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<RefreshTokenService> _log;

    public RefreshTokenService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        ITokenService tokens,
        IOptions<JwtOptions> options,
        TimeProvider time,
        ILogger<RefreshTokenService> log)
    {
        _db = db;
        _users = users;
        _tokens = tokens;
        _options = options.Value;
        _time = time;
        _log = log;
    }

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        // Limpieza barata: los tokens muertos de este usuario no hace falta conservarlos.
        var revokedBefore = now - KeepRevoked;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && (t.ExpiresAtUtc < now || (t.RevokedAtUtc != null && t.RevokedAtUtc < revokedBefore)))
            .ExecuteDeleteAsync(ct);

        var (issued, entity) = Create(userId, now);
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        return issued;
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var hash = Hash(refreshToken);

        var current = await _db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (current is null)
        {
            return Fail(RefreshFailure.Invalid);
        }

        if (current.RevokedAtUtc is { } revokedAt)
        {
            if (current.ReplacedByTokenId is null)
            {
                return Fail(RefreshFailure.Invalid); // cerrado con logout
            }

            if (now - revokedAt <= ConcurrencyGrace)
            {
                return Fail(RefreshFailure.Concurrent);
            }

            // Un token ya rotado vuelve a presentarse: alguien tiene una copia. Se cierran todas las sesiones del usuario.
            _log.LogWarning("[API] Refresh token reutilizado: se revocan las sesiones del usuario {UserId}", current.UserId);
            await _db.RefreshTokens
                .Where(t => t.UserId == current.UserId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
            return Fail(RefreshFailure.Invalid);
        }

        if (current.ExpiresAtUtc <= now)
        {
            return Fail(RefreshFailure.Invalid);
        }

        var user = await _users.FindByIdAsync(current.UserId.ToString());
        if (user is null || await _users.IsLockedOutAsync(user))
        {
            await RevokeByHashAsync(hash, now, ct);
            return Fail(RefreshFailure.Invalid);
        }

        var (issued, next) = Create(user.Id, now);

        // Atómico: solo una de dos peticiones simultáneas consigue rotar este token.
        var rotated = await _db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.ReplacedByTokenId, next.Id), ct);
        if (rotated == 0)
        {
            return Fail(RefreshFailure.Concurrent);
        }

        _db.RefreshTokens.Add(next);
        await _db.SaveChangesAsync(ct);

        var email = user.Email ?? "";
        var jwt = _tokens.CreateToken(user.Id, email);
        var auth = new AuthResponse(jwt.Token, jwt.ExpiresAtUtc, user.Id, email);
        return new RefreshResult(SessionResponse.From(auth, issued), RefreshFailure.None);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken ct = default) =>
        RevokeByHashAsync(Hash(refreshToken), _time.GetUtcNow().UtcDateTime, ct);

    private Task RevokeByHashAsync(string hash, DateTime now, CancellationToken ct) =>
        _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

    private (IssuedRefreshToken Issued, RefreshToken Entity) Create(Guid userId, DateTime now)
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddDays(_options.RefreshTokenDays);
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = expires
        };
        return (new IssuedRefreshToken(raw, expires), entity);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static RefreshResult Fail(RefreshFailure failure) => new(null, failure);
}
