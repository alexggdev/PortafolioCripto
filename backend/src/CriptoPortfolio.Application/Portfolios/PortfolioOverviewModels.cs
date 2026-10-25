namespace CriptoPortfolio.Application.Portfolios;

/// <summary>Una línea del resumen: cuánto vale cada portafolio del usuario.</summary>
public sealed record PortfolioValueDto(
    Guid Id,
    string Name,
    int PositionCount,
    decimal ValueUsd,
    decimal CostBasisUsd,
    decimal UnrealizedPnlUsd,
    decimal? UnrealizedPnlPct,
    decimal RealizedPnlUsd,
    decimal Change24hUsd,
    decimal? Change24hPct,
    // Falso: el portafolio se ve en el menú, pero no suma en el total, las monedas ni la asignación.
    bool IncludeInTotal = true);

/// <summary>Cuánto tiene un portafolio de una moneda.</summary>
public sealed record HolderShareDto(Guid PortfolioId, decimal Quantity);

/// <summary>Todos los portafolios del usuario sumados: totales, cada portafolio y las monedas juntas.</summary>
public sealed record PortfolioOverview(
    decimal ValueUsd,
    decimal CostBasisUsd,
    decimal UnrealizedPnlUsd,
    decimal? UnrealizedPnlPct,
    decimal RealizedPnlUsd,
    decimal Change24hUsd,
    decimal? Change24hPct,
    int UnpricedPositions,
    IReadOnlyList<PortfolioValueDto> Portfolios,
    // La misma moneda en varios portafolios aparece una sola vez, con las cantidades sumadas.
    IReadOnlyList<PositionDto> Positions,
    // Slug de cada moneda → portafolios que la tienen y cuánto (para desplegar el reparto de cada moneda).
    IReadOnlyDictionary<string, IReadOnlyList<HolderShareDto>> Holders);
