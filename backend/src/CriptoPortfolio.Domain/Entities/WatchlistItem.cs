namespace CriptoPortfolio.Domain.Entities;

/// <summary>Una moneda que el usuario sigue (la estrella). Clave: (UserId, AssetId).</summary>
public class WatchlistItem
{
    public Guid UserId { get; set; }
    public int AssetId { get; set; }
    public DateTime AddedAtUtc { get; set; }
}
