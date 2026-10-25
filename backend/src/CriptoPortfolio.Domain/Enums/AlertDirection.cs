namespace CriptoPortfolio.Domain.Enums;

/// <summary>Cuándo se dispara una alerta de precio.</summary>
public enum AlertDirection
{
    /// <summary>Cuando el precio llega al objetivo o lo supera.</summary>
    Above = 1,

    /// <summary>Cuando el precio llega al objetivo o cae por debajo.</summary>
    Below = 2
}
