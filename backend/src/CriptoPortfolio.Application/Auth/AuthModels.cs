using System.ComponentModel.DataAnnotations;

namespace CriptoPortfolio.Application.Auth;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    [StringLength(256, ErrorMessage = "El email es demasiado largo.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 128 caracteres.")]
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128)]
    public string Password { get; set; } = "";
}

public sealed record AuthToken(string Token, DateTime ExpiresAtUtc);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, Guid UserId, string Email);

public enum AuthFailure
{
    None = 0,
    ValidationFailed,
    InvalidCredentials,
    LockedOut
}

public sealed class AuthResult
{
    public bool Succeeded { get; private init; }
    public AuthResponse? Response { get; private init; }
    public AuthFailure Failure { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = Array.Empty<string>();

    public static AuthResult Ok(AuthResponse response) =>
        new() { Succeeded = true, Response = response };

    public static AuthResult Fail(AuthFailure failure, params string[] errors) =>
        new() { Succeeded = false, Failure = failure, Errors = errors };
}
