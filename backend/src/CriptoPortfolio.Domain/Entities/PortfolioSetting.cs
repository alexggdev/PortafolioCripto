namespace CriptoPortfolio.Domain.Entities;

/// <summary>Ajustes de un portafolio que no son datos contables: imagen, orden en el menú y cómo cuenta en el total.</summary>
public class PortfolioSetting
{
    public Guid PortfolioId { get; set; }

    /// <summary>Número de la imagen elegida; null si no eligió ninguna (el cliente usa una según el id).</summary>
    public int? Avatar { get; set; }

    /// <summary>Posición en el menú (0 es la primera). Sin ajuste propio, va al final.</summary>
    public int SortOrder { get; set; } = int.MaxValue;

    /// <summary>Si su valor y sus monedas suman en la vista general.</summary>
    public bool IncludeInTotal { get; set; } = true;

    /// <summary>El que se abre al entrar en el portafolio. Como mucho uno por usuario.</summary>
    public bool IsDefault { get; set; }
}
