using System.ComponentModel.DataAnnotations;

namespace CriptoPortfolio.Application.Auth;

public sealed class RefreshRequest
{
    [Required, MaxLength(200)]
    public string RefreshToken { get; set; } = "";
}

public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Lo que recibe la web tras entrar o renovar: el JWT de siempre más el refresh token.</summary>
public sealed record SessionResponse(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc)
{
    public static SessionResponse From(AuthResponse auth, IssuedRefreshToken refresh) =>
        new(auth.Token, auth.ExpiresAtUtc, auth.UserId, auth.Email, refresh.Token, refresh.ExpiresAtUtc);
}

public enum RefreshFailure
{
    None,

    /// <summary>Desconocido, caducado, revocado o usuario bloqueado: hay que volver a entrar.</summary>
    Invalid,

    /// <summary>Otra petición acaba de renovar ese mismo token: no es un ataque, basta con reintentar.</summary>
    Concurrent
}

public sealed record RefreshResult(SessionResponse? Session, RefreshFailure Failure)
{
    public bool Succeeded => Session is not null;
}

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Rota el token: el recibido deja de valer y se entrega uno nuevo junto a un JWT nuevo.</summary>
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Cierra la sesión de ese token. No falla si no existe.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}
