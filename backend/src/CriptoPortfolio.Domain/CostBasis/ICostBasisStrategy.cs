using CriptoPortfolio.Domain.Entities;

namespace CriptoPortfolio.Domain.CostBasis;

/// <summary>
/// Cómo se reparte el coste cuando se vende una parte de lo comprado (FIFO, media ponderada...).
/// Es lógica pura: no sabe nada de EF ni de HTTP, así que se prueba con listas en memoria.
/// </summary>
public interface ICostBasisStrategy
{
    string Name { get; }

    /// <summary>
    /// Repasa las transacciones de UNA moneda (en cualquier orden; se ordenan por fecha) y devuelve lo que queda,
    /// su coste y la ganancia ya realizada. Lanza <see cref="InsufficientHoldingsException"/> si en algún momento
    /// se sacan más unidades de las que había.
    /// </summary>
    CostBasisResult Compute(IEnumerable<Transaction> transactions);
}
