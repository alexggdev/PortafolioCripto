using Microsoft.AspNetCore.Identity;

namespace CriptoPortfolio.Infrastructure.Identity;

/// <summary>Mensajes de Identity en español (los duplicados los resume AuthService).</summary>
public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"La contraseña debe tener al menos {length} caracteres."
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "La contraseña debe incluir al menos un número."
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "La contraseña debe incluir al menos una letra minúscula."
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "La contraseña debe incluir al menos una letra mayúscula."
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "La contraseña debe incluir al menos un símbolo."
    };

    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = "Ha ocurrido un error."
    };
}
