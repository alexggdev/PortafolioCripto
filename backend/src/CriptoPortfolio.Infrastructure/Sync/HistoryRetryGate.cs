using System.Collections.Concurrent;

namespace CriptoPortfolio.Infrastructure.Sync;

/// <summary>
/// Memoria (por proceso) de las monedas a las que no hay que volver a preguntar hasta cierta hora:
/// las que fallaron o no devolvieron datos. Evita gastar llamadas (y créditos de CoinGecko) cada pocos minutos.
/// Es un singleton; si la aplicación se reinicia, se olvida y se vuelve a probar, que es lo deseado.
/// </summary>
public sealed class HistoryRetryGate
{
    private readonly ConcurrentDictionary<int, DateTime> _blockedUntil = new();

    public bool IsBlocked(int assetId, DateTime nowUtc) =>
        _blockedUntil.TryGetValue(assetId, out var until) && nowUtc < until;

    public void Block(int assetId, DateTime untilUtc) => _blockedUntil[assetId] = untilUtc;
}
