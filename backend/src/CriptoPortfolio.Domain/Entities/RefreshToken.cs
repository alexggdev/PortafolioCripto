namespace CriptoPortfolio.Domain.Entities;

/// <summary>
/// Credencial de larga duración para renovar el JWT sin volver a pedir la contraseña.
/// Solo se guarda el hash: si alguien viera la base de datos, no podría usar los tokens.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) del token que recibe el cliente.</summary>
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Token que lo sustituyó al rotarlo. Null si se revocó por logout o por seguridad.</summary>
    public Guid? ReplacedByTokenId { get; set; }
}
