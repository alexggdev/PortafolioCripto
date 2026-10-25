namespace CriptoPortfolio.Application.Portfolios;

public sealed record PortfolioInfo(
    Guid Id,
    string Name,
    DateTime CreatedAtUtc,
    int TransactionCount,
    int? Avatar = null,
    bool IncludeInTotal = true,
    bool IsDefault = false);

/// <summary>Cambios sobre un portafolio. Lo que va a null se deja como está.</summary>
public sealed record PortfolioChanges(string? Name = null, int? Avatar = null, bool? IncludeInTotal = null, bool? IsDefault = null);

public enum PortfolioFailure
{
    None,
    Invalid,
    NotFound
}

public sealed record PortfolioResult(PortfolioFailure Failure, PortfolioInfo? Portfolio, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Failure == PortfolioFailure.None;

    public static PortfolioResult Ok(PortfolioInfo? portfolio) =>
        new(PortfolioFailure.None, portfolio, Array.Empty<string>());

    public static PortfolioResult Fail(PortfolioFailure failure, params string[] errors) =>
        new(failure, null, errors);
}

/// <summary>Crear, cambiar, duplicar, ordenar y borrar los portafolios del usuario. Todo se limita a los del usuario indicado.</summary>
public interface IPortfolioManager
{
    /// <summary>En el orden que eligió el usuario; los que no tienen orden propio, al final por antigüedad.</summary>
    Task<IReadOnlyList<PortfolioInfo>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<PortfolioResult> CreateAsync(Guid userId, string? name, int? avatar = null, CancellationToken ct = default);

    Task<PortfolioResult> RenameAsync(Guid userId, Guid id, string? name, CancellationToken ct = default);

    Task<PortfolioResult> UpdateAsync(Guid userId, Guid id, PortfolioChanges changes, CancellationToken ct = default);

    /// <summary>Copia exacta: mismas transacciones, imagen y ajuste de «contabilizar». Nunca queda como predeterminado.</summary>
    Task<PortfolioResult> DuplicateAsync(Guid userId, Guid id, string? name, CancellationToken ct = default);

    /// <summary>Fija el orden del menú. Hay que enviar todos los portafolios del usuario, cada uno una vez.</summary>
    Task<PortfolioResult> ReorderAsync(Guid userId, IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>Borra el portafolio con sus transacciones y su historial. No se puede deshacer.</summary>
    Task<PortfolioResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
