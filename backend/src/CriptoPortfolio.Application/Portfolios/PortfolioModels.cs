using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Application.Portfolios;

/// <summary>Lo que envía el cliente al crear o editar una transacción. Todo en USD; la fecha en UTC.</summary>
public sealed record TransactionRequest(
    string? AssetSlug,
    TransactionType Type,
    decimal Quantity,
    decimal? UnitPriceUsd,
    decimal? FeeUsd,
    DateTime DateUtc,
    string? Note,
    // Solo al crear: en qué portafolio se anota. Sin él, el primero del usuario.
    Guid? PortfolioId = null);

public sealed record TransactionDto(
    long Id,
    string AssetSlug,
    string Symbol,
    string Name,
    string? ImageUrl,
    TransactionType Type,
    decimal Quantity,
    decimal UnitPriceUsd,
    decimal FeeUsd,
    DateTime DateUtc,
    string? Note);

public sealed record TransactionListQuery(
    string? AssetSlug = null, int Page = 1, int PageSize = 50, Guid? PortfolioId = null, TransactionType? Type = null)
{
    public const int MaxPageSize = 100;
}

/// <summary>Una moneda que el usuario conserva, valorada al precio actual (null si la moneda no tiene precio).</summary>
public sealed record PositionDto(
    int AssetId,
    string Slug,
    string Symbol,
    string Name,
    string? ImageUrl,
    decimal Quantity,
    decimal AverageCostUsd,
    decimal CostBasisUsd,
    decimal? PriceUsd,
    string? PriceSource,
    DateTime? PriceUpdatedAtUtc,
    decimal? ValueUsd,
    decimal? UnrealizedPnlUsd,
    decimal? UnrealizedPnlPct,
    decimal RealizedPnlUsd,
    decimal? AllocationPct,
    decimal? Change24hPct,
    decimal? Change1hPct = null,
    decimal? Change7dPct = null);

/// <summary>Mover una moneda (con todo su historial) de un portafolio a otro del mismo usuario.</summary>
public sealed record MoveAssetRequest(Guid FromPortfolioId, Guid ToPortfolioId);

public sealed record PortfolioSummary(
    Guid? PortfolioId,
    string Name,
    // Valor actual de las posiciones con precio.
    decimal ValueUsd,
    // Coste de todo lo que se conserva (con precio o sin él).
    decimal CostBasisUsd,
    decimal UnrealizedPnlUsd,
    decimal? UnrealizedPnlPct,
    // Ganancia ya cobrada con ventas (incluye posiciones cerradas).
    decimal RealizedPnlUsd,
    decimal TotalPnlUsd,
    decimal Change24hUsd,
    decimal? Change24hPct,
    // Posiciones sin precio: no suman al valor ni a la ganancia no realizada.
    int UnpricedPositions,
    // Monedas cuyo historial no cuadra (se sacaron más unidades de las que había). No debería pasar.
    int InconsistentAssets,
    string CostBasisMethod,
    IReadOnlyList<PositionDto> Positions);

public enum TransactionFailure
{
    None,
    Invalid,
    AssetNotFound,
    NotFound
}

public sealed record TransactionResult(TransactionFailure Failure, TransactionDto? Transaction, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Failure == TransactionFailure.None;

    public static TransactionResult Ok(TransactionDto? transaction) =>
        new(TransactionFailure.None, transaction, Array.Empty<string>());

    public static TransactionResult Fail(TransactionFailure failure, params string[] errors) =>
        new(failure, null, errors);
}
