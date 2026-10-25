namespace CriptoPortfolio.Application.Catalog;

public enum ChartRange
{
    Week,
    Month,
    ThreeMonths,
    Year,
    All
}

public static class ChartRanges
{
    /// <summary>Acepta 7D, 1M, 3M, 1Y y All, sin distinguir mayúsculas. (No hay 1D: no se guardan precios por horas.)</summary>
    public static bool TryParse(string? value, out ChartRange range)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "7D":
                range = ChartRange.Week;
                return true;
            case "1M":
                range = ChartRange.Month;
                return true;
            case "3M":
                range = ChartRange.ThreeMonths;
                return true;
            case "1Y":
                range = ChartRange.Year;
                return true;
            case "ALL":
                range = ChartRange.All;
                return true;
            default:
                range = default;
                return false;
        }
    }

    /// <summary>Días hacia atrás; null para "todo".</summary>
    public static int? Days(ChartRange range) => range switch
    {
        ChartRange.Week => 7,
        ChartRange.Month => 30,
        ChartRange.ThreeMonths => 90,
        ChartRange.Year => 365,
        _ => null
    };

    public static string Code(ChartRange range) => range switch
    {
        ChartRange.Week => "7D",
        ChartRange.Month => "1M",
        ChartRange.ThreeMonths => "3M",
        ChartRange.Year => "1Y",
        _ => "All"
    };
}

public sealed record ChartPoint(DateOnly Date, decimal PriceUsd);

/// <summary>
/// Serie para el gráfico de una moneda: cierres diarios y, al final, el precio actual (fecha de hoy).
/// </summary>
/// <param name="ChangePct">Variación entre el primer y el último punto, en %. Null si hay menos de dos puntos.</param>
/// <param name="IncludesCurrentPrice">True si el último punto es el precio actual y no un cierre.</param>
public sealed record AssetChart(
    string Slug,
    string Range,
    IReadOnlyList<ChartPoint> Points,
    decimal? ChangePct,
    bool IncludesCurrentPrice);
