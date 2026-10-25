using System.Globalization;
using System.Text;

namespace CriptoPortfolio.Application.Portfolios;

public enum CsvFormat
{
    /// <summary>Coma como separador y punto decimal (RFC 4180). Para importar en otras herramientas.</summary>
    Standard,

    /// <summary>Punto y coma y coma decimal, con BOM UTF-8: lo que espera Excel en configuración regional española.</summary>
    Excel
}

/// <summary>Exportación de transacciones a CSV. Función pura: no toca la base de datos ni el reloj.</summary>
public static class TransactionCsv
{
    private static readonly string[] Header =
    {
        "Fecha (UTC)", "Tipo", "Moneda", "Símbolo", "Cantidad", "Precio unitario (USD)", "Comisión (USD)", "Nota"
    };

    private const string DecimalPattern = "0.############################";

    public static byte[] ToBytes(IEnumerable<TransactionDto> transactions, CsvFormat format)
    {
        var text = ToText(transactions, format);
        var body = Encoding.UTF8.GetBytes(text);
        if (format != CsvFormat.Excel)
        {
            return body;
        }

        // Excel solo reconoce UTF-8 (tildes, ñ) si el archivo empieza por la marca BOM.
        return Encoding.UTF8.GetPreamble().Concat(body).ToArray();
    }

    public static string ToText(IEnumerable<TransactionDto> transactions, CsvFormat format)
    {
        var separator = format == CsvFormat.Excel ? ';' : ',';
        var culture = format == CsvFormat.Excel ? CultureInfo.GetCultureInfo("es-ES") : CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        AppendRow(sb, separator, Header);
        foreach (var t in transactions)
        {
            AppendRow(sb, separator, new[]
            {
                // DateUtc ya es UTC: el sufijo Z es literal para que no dependa del Kind que traiga EF.
                t.DateUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                t.Type.ToString(),
                Safe(t.Name),
                Safe(t.Symbol.ToUpperInvariant()),
                Plain(t.Quantity, culture),
                Plain(t.UnitPriceUsd, culture),
                Plain(t.FeeUsd, culture),
                Safe(t.Note)
            });
        }

        return sb.ToString();
    }

    /// <summary>Nombre de archivo seguro: solo ASCII, sin espacios ni barras.</summary>
    public static string FileName(string? portfolioName, DateTime dateUtc)
    {
        var slug = new StringBuilder();
        foreach (var c in (portfolioName ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // quita la tilde y deja la letra base
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(lower);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }

            if (slug.Length >= 40)
            {
                break;
            }
        }

        var name = slug.ToString().Trim('-');
        if (name.Length == 0)
        {
            name = "portafolio";
        }

        return $"transacciones-{name}-{dateUtc:yyyyMMdd}.csv";
    }

    private static string Plain(decimal value, CultureInfo culture) => value.ToString(DecimalPattern, culture);

    /// <summary>
    /// Evita la inyección de fórmulas: una celda de texto que empieza por = + - @ (o tabulador / retorno)
    /// se ejecutaría como fórmula al abrirla en Excel. Se antepone una comilla simple.
    /// </summary>
    private static string Safe(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    private static void AppendRow(StringBuilder sb, char separator, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(Escape(fields[i], separator));
        }

        sb.Append("\r\n");
    }

    private static string Escape(string field, char separator)
    {
        if (field.IndexOfAny(new[] { separator, '"', '\r', '\n' }) < 0)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
