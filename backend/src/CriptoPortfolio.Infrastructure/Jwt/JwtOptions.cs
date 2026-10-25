using System.Text;

namespace CriptoPortfolio.Infrastructure.Jwt;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CriptoPortfolio";
    public string Audience { get; set; } = "CriptoPortfolio.Web";

    /// <summary>Clave HMAC-SHA256. Nunca en el repositorio: user-secrets o variable de entorno Jwt__SigningKey.</summary>
    public string SigningKey { get; set; } = "";

    /// <summary>Vida del JWT. Con refresh token conviene que sea corta (15 min): si se filtra, caduca pronto.</summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>Vida del refresh token: cuánto puede pasar sin visitar la web antes de tener que volver a entrar.</summary>
    public int RefreshTokenDays { get; set; } = 14;

    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            error = "Jwt:SigningKey debe tener al menos 32 bytes (configúrala con user-secrets o la variable Jwt__SigningKey).";
            return false;
        }

        if (ExpirationMinutes is < 1 or > 1440)
        {
            error = "Jwt:ExpirationMinutes debe estar entre 1 y 1440.";
            return false;
        }

        if (RefreshTokenDays is < 1 or > 90)
        {
            error = "Jwt:RefreshTokenDays debe estar entre 1 y 90.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            error = "Jwt:Issuer y Jwt:Audience son obligatorios.";
            return false;
        }

        error = null;
        return true;
    }

    public void EnsureValid()
    {
        if (!IsValid(out var error))
        {
            throw new InvalidOperationException(error);
        }
    }
}
