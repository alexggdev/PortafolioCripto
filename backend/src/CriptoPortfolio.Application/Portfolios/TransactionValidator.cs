using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Application.Portfolios;

/// <summary>Valores ya limpios y listos para guardar.</summary>
public sealed record ValidatedTransaction(
    string AssetSlug,
    TransactionType Type,
    decimal Quantity,
    decimal UnitPriceUsd,
    decimal FeeUsd,
    DateTime DateUtc,
    string? Note);

public sealed record TransactionValidation(ValidatedTransaction? Value, IReadOnlyList<string> Errors)
{
    public bool IsValid => Value is not null && Errors.Count == 0;
}

/// <summary>Validación de entrada pura (sin base de datos). Los límites coinciden con las columnas de SQL.</summary>
public static class TransactionValidator
{
    public const int MaxSlugLength = 200;
    public const int MaxNoteLength = 500;

    // Quantity decimal(28,12): 16 cifras enteras; precio decimal(28,18): 10; comisión decimal(28,8).
    public const decimal MaxQuantity = 999_999_999_999_999m;
    public const decimal MaxMoney = 9_999_999_999m;
    public const int QuantityScale = 12;
    public const int PriceScale = 18;
    public const int FeeScale = 8;

    /// <summary>Génesis de Bitcoin: ninguna transacción de cripto es anterior.</summary>
    public static readonly DateTime EarliestDateUtc = new(2009, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public static TransactionValidation Validate(TransactionRequest request, DateTime nowUtc)
    {
        var errors = new List<string>();

        var slug = (request.AssetSlug ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0)
        {
            errors.Add("Elige una moneda.");
        }
        else if (slug.Length > MaxSlugLength)
        {
            errors.Add("La moneda no es válida.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            errors.Add("El tipo de transacción no es válido.");
        }

        var unitPrice = request.UnitPriceUsd ?? 0m;
        var fee = request.FeeUsd ?? 0m;

        if (request.Quantity <= 0)
        {
            errors.Add("La cantidad debe ser mayor que cero.");
        }
        else if (request.Quantity > MaxQuantity)
        {
            errors.Add("La cantidad es demasiado grande.");
        }
        else if (ScaleOf(request.Quantity) > QuantityScale)
        {
            errors.Add($"La cantidad admite como máximo {QuantityScale} decimales.");
        }

        if (unitPrice < 0)
        {
            errors.Add("El precio no puede ser negativo.");
        }
        else if (unitPrice > MaxMoney)
        {
            errors.Add("El precio es demasiado grande.");
        }
        else if (ScaleOf(unitPrice) > PriceScale)
        {
            errors.Add($"El precio admite como máximo {PriceScale} decimales.");
        }
        else if (unitPrice == 0 && request.Type is TransactionType.Buy or TransactionType.Sell)
        {
            errors.Add("En compras y ventas el precio debe ser mayor que cero.");
        }

        if (fee < 0)
        {
            errors.Add("La comisión no puede ser negativa.");
        }
        else if (fee > MaxMoney)
        {
            errors.Add("La comisión es demasiado grande.");
        }
        else if (ScaleOf(fee) > FeeScale)
        {
            errors.Add($"La comisión admite como máximo {FeeScale} decimales.");
        }

        var date = request.DateUtc.Kind switch
        {
            DateTimeKind.Local => request.DateUtc.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(request.DateUtc, DateTimeKind.Utc),
            _ => request.DateUtc
        };

        if (date < EarliestDateUtc)
        {
            errors.Add("La fecha es anterior a la existencia de Bitcoin.");
        }
        else if (date > nowUtc + FutureTolerance)
        {
            errors.Add("La fecha no puede ser futura.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is not null && note.Length > MaxNoteLength)
        {
            errors.Add($"La nota admite como máximo {MaxNoteLength} caracteres.");
        }

        return errors.Count > 0
            ? new TransactionValidation(null, errors)
            : new TransactionValidation(
                new ValidatedTransaction(slug, request.Type, request.Quantity, unitPrice, fee, date, note),
                errors);
    }

    /// <summary>Decimales significativos (1,50 cuenta como 1, no como 2).</summary>
    public static int ScaleOf(decimal value)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        return dot < 0 ? 0 : text.TrimEnd('0').Length - dot - 1;
    }
}
