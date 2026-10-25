using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CriptoPortfolio.Infrastructure.Persistence;

/// <summary>
/// SQL Server no guarda el Kind de un DateTime. Estos convertidores garantizan que todo lo que
/// entra se guarda como UTC y todo lo que sale vuelve con Kind = Utc (así el JSON lleva la "Z").
/// Local se convierte a UTC; Unspecified se interpreta como UTC.
/// </summary>
public static class UtcDateTimes
{
    public static DateTime Normalize(DateTime value) =>
        value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? NormalizeNullable(DateTime? value) =>
        value.HasValue ? Normalize(value.Value) : null;

    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtcNullable(DateTime? value) =>
        value.HasValue ? AsUtc(value.Value) : null;
}

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => UtcDateTimes.Normalize(v), v => UtcDateTimes.AsUtc(v))
    {
    }
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(v => UtcDateTimes.NormalizeNullable(v), v => UtcDateTimes.AsUtcNullable(v))
    {
    }
}
