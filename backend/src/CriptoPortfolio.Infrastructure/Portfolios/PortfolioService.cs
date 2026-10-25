using System.Globalization;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Portfolios;

public sealed class PortfolioService : IPortfolioService
{
    private readonly IAppDbContext _db;
    private readonly ILivePriceCache _cache;
    private readonly ICostBasisStrategy _strategy;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _live;
    private readonly PortfolioOptions _options;
    private readonly ILogger<PortfolioService> _log;
    private readonly IPortfolioHistoryInvalidator? _history;

    public PortfolioService(
        IAppDbContext db,
        ILivePriceCache cache,
        ICostBasisStrategy strategy,
        TimeProvider time,
        IOptions<LivePriceOptions> live,
        IOptions<PortfolioOptions> options,
        ILogger<PortfolioService> log,
        IPortfolioHistoryInvalidator? history = null)
    {
        _db = db;
        _cache = cache;
        _strategy = strategy;
        _time = time;
        _live = live.Value;
        _options = options.Value;
        _log = log;
        _history = history;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- lectura

    public async Task<PortfolioSummary> GetSummaryAsync(Guid userId, Guid? portfolioId = null, CancellationToken ct = default)
    {
        var portfolio = await FindPortfolioAsync(userId, portfolioId, ct);
        if (portfolio is null)
        {
            return EmptySummary(null, _options.DefaultPortfolioName);
        }

        var transactions = await _db.Transactions.AsNoTracking()
            .Where(t => t.PortfolioId == portfolio.Id)
            .ToListAsync(ct);

        var assetIds = transactions.Select(t => t.AssetId).Distinct().ToList();
        var assets = await _db.Assets.AsNoTracking()
            .Include(a => a.Price)
            .Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var now = Now;
        var positions = new List<PositionDto>();
        var realizedTotal = 0m;
        var inconsistent = 0;

        foreach (var group in transactions.GroupBy(t => t.AssetId))
        {
            if (!assets.TryGetValue(group.Key, out var asset))
            {
                continue; // la clave foránea lo impide; por si acaso
            }

            CostBasisResult result;
            try
            {
                result = _strategy.Compute(group);
            }
            catch (InsufficientHoldingsException ex)
            {
                inconsistent++;
                _log.LogError(ex, "[API] Historial incoherente en el portafolio {PortfolioId}, moneda {Asset}", portfolio.Id, asset.Slug);
                continue;
            }

            realizedTotal += result.RealizedPnlUsd;
            if (result.Quantity <= 0)
            {
                continue; // posición cerrada: solo cuenta su ganancia realizada
            }

            var (price, source, updated) = ResolvePrice(asset, now);
            var value = price is null ? (decimal?)null : price.Value * result.Quantity;
            var pnl = value is null ? (decimal?)null : value.Value - result.CostBasisUsd;

            positions.Add(new PositionDto(
                asset.Id, asset.Slug, asset.Symbol, asset.Name, asset.ImageUrl,
                result.Quantity, result.AverageCostUsd, result.CostBasisUsd,
                price, source, updated,
                value, pnl, Percent(pnl, result.CostBasisUsd),
                result.RealizedPnlUsd, null, asset.Price?.Change24hPct,
                asset.Price?.Change1hPct, asset.Price?.Change7dPct));
        }

        var priced = positions.Where(p => p.ValueUsd is not null).ToList();
        var value24 = priced.Sum(p => p.ValueUsd!.Value);
        var pricedCost = priced.Sum(p => p.CostBasisUsd);
        var unrealized = value24 - pricedCost;

        // Variación de 24 h: valor ahora menos valor que tenían esas unidades hace 24 h (según el % de cada moneda).
        var change24 = 0m;
        var previous24 = 0m;
        foreach (var p in priced)
        {
            if (p.Change24hPct is { } pct && pct > -100m)
            {
                var before = p.ValueUsd!.Value / (1m + pct / 100m);
                change24 += p.ValueUsd.Value - before;
                previous24 += before;
            }
        }

        var ordered = positions
            .Select(p => p with { AllocationPct = p.ValueUsd is null || value24 <= 0 ? null : p.ValueUsd / value24 * 100m })
            .OrderByDescending(p => p.ValueUsd ?? -1m)
            .ThenBy(p => p.Symbol)
            .ToList();

        return new PortfolioSummary(
            portfolio.Id, portfolio.Name,
            value24, positions.Sum(p => p.CostBasisUsd),
            unrealized, Percent(unrealized, pricedCost),
            realizedTotal, unrealized + realizedTotal,
            change24, previous24 > 0 ? change24 / previous24 * 100m : null,
            positions.Count - priced.Count, inconsistent,
            _strategy.Name, ordered);
    }

    public async Task<PortfolioOverview> GetOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        var portfolios = await _db.Portfolios.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var excluded = (await _db.PortfolioSettings.AsNoTracking()
                .Where(s => !s.IncludeInTotal && _db.Portfolios.Any(p => p.Id == s.PortfolioId && p.UserId == userId))
                .Select(s => s.PortfolioId)
                .ToListAsync(ct))
            .ToHashSet();

        var lines = new List<PortfolioValueDto>();
        var all = new List<PositionDto>();
        var holders = new Dictionary<string, List<HolderShareDto>>(); // moneda → portafolios que la tienen
        var realized = 0m;
        foreach (var p in portfolios)
        {
            var summary = await GetSummaryAsync(userId, p.Id, ct);
            var included = !excluded.Contains(p.Id);
            if (included)
            {
                realized += summary.RealizedPnlUsd;
                all.AddRange(summary.Positions);
                foreach (var position in summary.Positions)
                {
                    if (!holders.TryGetValue(position.Slug, out var owners))
                    {
                        holders[position.Slug] = owners = new List<HolderShareDto>();
                    }

                    owners.Add(new HolderShareDto(p.Id, position.Quantity));
                }
            }

            lines.Add(new PortfolioValueDto(
                p.Id, p.Name, summary.Positions.Count, summary.ValueUsd, summary.CostBasisUsd,
                summary.UnrealizedPnlUsd, summary.UnrealizedPnlPct, summary.RealizedPnlUsd,
                summary.Change24hUsd, summary.Change24hPct, included));
        }

        // La misma moneda en varios portafolios se junta en una sola posición.
        var merged = all.GroupBy(x => x.AssetId).Select(g =>
        {
            var first = g.First();
            var quantity = g.Sum(x => x.Quantity);
            var cost = g.Sum(x => x.CostBasisUsd);
            var value = first.PriceUsd is null ? (decimal?)null : first.PriceUsd.Value * quantity;
            var pnl = value is null ? (decimal?)null : value.Value - cost;
            return first with
            {
                Quantity = quantity,
                CostBasisUsd = cost,
                AverageCostUsd = quantity > 0 ? cost / quantity : 0m,
                ValueUsd = value,
                UnrealizedPnlUsd = pnl,
                UnrealizedPnlPct = Percent(pnl, cost),
                RealizedPnlUsd = g.Sum(x => x.RealizedPnlUsd)
            };
        }).ToList();

        var priced = merged.Where(x => x.ValueUsd is not null).ToList();
        var value24 = priced.Sum(x => x.ValueUsd!.Value);
        var pricedCost = priced.Sum(x => x.CostBasisUsd);
        var unrealized = value24 - pricedCost;

        var change24 = 0m;
        var previous24 = 0m;
        foreach (var x in priced)
        {
            if (x.Change24hPct is { } pct && pct > -100m)
            {
                var before = x.ValueUsd!.Value / (1m + pct / 100m);
                change24 += x.ValueUsd.Value - before;
                previous24 += before;
            }
        }

        var ordered = merged
            .Select(x => x with { AllocationPct = x.ValueUsd is null || value24 <= 0 ? null : x.ValueUsd / value24 * 100m })
            .OrderByDescending(x => x.ValueUsd ?? -1m)
            .ThenBy(x => x.Symbol)
            .ToList();

        return new PortfolioOverview(
            value24, merged.Sum(x => x.CostBasisUsd), unrealized, Percent(unrealized, pricedCost), realized,
            change24, previous24 > 0 ? change24 / previous24 * 100m : null,
            merged.Count - priced.Count, lines, ordered,
            holders.ToDictionary(h => h.Key, h => (IReadOnlyList<HolderShareDto>)h.Value));
    }

    public async Task<PagedResult<TransactionDto>> ListTransactionsAsync(
        Guid userId, TransactionListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, TransactionListQuery.MaxPageSize);

        var portfolio = await FindPortfolioAsync(userId, query.PortfolioId, ct);
        if (portfolio is null)
        {
            return new PagedResult<TransactionDto>(Array.Empty<TransactionDto>(), page, pageSize, 0);
        }

        var rows = from t in _db.Transactions.AsNoTracking()
                   join a in _db.Assets.AsNoTracking() on t.AssetId equals a.Id
                   where t.PortfolioId == portfolio.Id
                   select new { t, a };

        var slug = query.AssetSlug?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(slug))
        {
            rows = rows.Where(x => x.a.Slug == slug);
        }

        if (query.Type is { } type)
        {
            rows = rows.Where(x => x.t.Type == type);
        }

        var total = await rows.CountAsync(ct);
        var page1 = await rows
            .OrderByDescending(x => x.t.DateUtc).ThenByDescending(x => x.t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>(page1.Select(x => ToDto(x.t, x.a)).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<TransactionDto>> ExportTransactionsAsync(
        Guid userId, Guid? portfolioId = null, CancellationToken ct = default)
    {
        var portfolio = await FindPortfolioAsync(userId, portfolioId, ct);
        if (portfolio is null)
        {
            return Array.Empty<TransactionDto>();
        }

        // El tope por usuario (MaxTransactionsPerUser) acota el tamaño: cabe en memoria.
        var rows = await (from t in _db.Transactions.AsNoTracking()
                          join a in _db.Assets.AsNoTracking() on t.AssetId equals a.Id
                          where t.PortfolioId == portfolio.Id
                          select new { t, a })
            .OrderBy(x => x.t.DateUtc).ThenBy(x => x.t.Id)
            .ToListAsync(ct);

        return rows.Select(x => ToDto(x.t, x.a)).ToList();
    }

    // -------------------------------------------------------------- escritura

    public async Task<TransactionResult> AddTransactionAsync(Guid userId, TransactionRequest request, CancellationToken ct = default)
    {
        var validation = TransactionValidator.Validate(request, Now);
        if (!validation.IsValid)
        {
            return Invalid(validation.Errors.ToArray());
        }

        var v = validation.Value!;
        var asset = await FindAssetAsync(v.AssetSlug, ct);
        if (asset is null)
        {
            return TransactionResult.Fail(TransactionFailure.AssetNotFound, "La moneda no existe.");
        }

        var portfolio = await GetOrCreatePortfolioAsync(userId, request.PortfolioId, ct);
        if (portfolio is null)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "El portafolio no existe.");
        }

        var count = await _db.Transactions.CountAsync(
            t => _db.Portfolios.Any(p => p.Id == t.PortfolioId && p.UserId == userId), ct);
        if (count >= _options.MaxTransactionsPerUser)
        {
            return Invalid($"Has alcanzado el máximo de {_options.MaxTransactionsPerUser} transacciones.");
        }

        var entity = Build(portfolio.Id, asset.Id, v);
        var history = (await LoadAssetTransactionsAsync(portfolio.Id, asset.Id, ct)).Append(entity).ToList();
        var problem = CheckHistory(asset, history);
        if (problem is not null)
        {
            return Invalid(problem);
        }

        _db.Transactions.Add(entity);
        await StageHistoryInvalidationAsync(portfolio.Id, v.DateUtc, ct);
        await _db.SaveChangesAsync(ct);

        return TransactionResult.Ok(ToDto(entity, asset));
    }

    public async Task<TransactionResult> UpdateTransactionAsync(
        Guid userId, long id, TransactionRequest request, CancellationToken ct = default)
    {
        var validation = TransactionValidator.Validate(request, Now);
        if (!validation.IsValid)
        {
            return Invalid(validation.Errors.ToArray());
        }

        var v = validation.Value!;
        var entity = await FindOwnedTransactionAsync(userId, id, ct);
        if (entity is null)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "La transacción no existe.");
        }

        var asset = await FindAssetAsync(v.AssetSlug, ct);
        if (asset is null)
        {
            return TransactionResult.Fail(TransactionFailure.AssetNotFound, "La moneda no existe.");
        }

        // El historial de la moneda nueva con el cambio aplicado debe cuadrar...
        var candidate = Build(entity.PortfolioId, asset.Id, v);
        candidate.Id = entity.Id;
        var newHistory = (await LoadAssetTransactionsAsync(entity.PortfolioId, asset.Id, ct))
            .Where(t => t.Id != id).Append(candidate).ToList();
        var problem = CheckHistory(asset, newHistory);
        if (problem is not null)
        {
            return Invalid(problem);
        }

        // ...y si cambia de moneda, el de la moneda antigua sin esta transacción también.
        if (entity.AssetId != asset.Id)
        {
            var oldAsset = await _db.Assets.AsNoTracking().FirstAsync(a => a.Id == entity.AssetId, ct);
            var oldHistory = (await LoadAssetTransactionsAsync(entity.PortfolioId, oldAsset.Id, ct))
                .Where(t => t.Id != id).ToList();
            var oldProblem = CheckHistory(oldAsset, oldHistory);
            if (oldProblem is not null)
            {
                return Invalid(oldProblem);
            }
        }

        var firstAffected = entity.DateUtc < v.DateUtc ? entity.DateUtc : v.DateUtc;
        entity.AssetId = asset.Id;
        entity.Type = v.Type;
        entity.Quantity = v.Quantity;
        entity.UnitPriceUsd = v.UnitPriceUsd;
        entity.FeeUsd = v.FeeUsd;
        entity.DateUtc = v.DateUtc;
        entity.Note = v.Note;
        await StageHistoryInvalidationAsync(entity.PortfolioId, firstAffected, ct);
        await _db.SaveChangesAsync(ct);

        return TransactionResult.Ok(ToDto(entity, asset));
    }

    public async Task<TransactionResult> DeleteTransactionAsync(Guid userId, long id, CancellationToken ct = default)
    {
        var entity = await FindOwnedTransactionAsync(userId, id, ct);
        if (entity is null)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "La transacción no existe.");
        }

        var asset = await _db.Assets.AsNoTracking().FirstAsync(a => a.Id == entity.AssetId, ct);
        var remaining = (await LoadAssetTransactionsAsync(entity.PortfolioId, asset.Id, ct)).Where(t => t.Id != id).ToList();
        var problem = CheckHistory(asset, remaining);
        if (problem is not null)
        {
            return Invalid("No se puede borrar: " + problem);
        }

        _db.Transactions.Remove(entity);
        await StageHistoryInvalidationAsync(entity.PortfolioId, entity.DateUtc, ct);
        await _db.SaveChangesAsync(ct);

        return TransactionResult.Ok(null);
    }

    public async Task<TransactionResult> RemoveAssetAsync(
        Guid userId, string assetSlug, Guid? portfolioId, CancellationToken ct = default)
    {
        var portfolio = await FindPortfolioAsync(userId, portfolioId, ct);
        if (portfolio is null)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "El portafolio no existe.");
        }

        var asset = await FindAssetAsync(NormalizeSlug(assetSlug), ct);
        if (asset is null)
        {
            return TransactionResult.Fail(TransactionFailure.AssetNotFound, "La moneda no existe.");
        }

        var transactions = await _db.Transactions
            .Where(t => t.PortfolioId == portfolio.Id && t.AssetId == asset.Id)
            .ToListAsync(ct);
        if (transactions.Count == 0)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "Esa moneda no está en el portafolio.");
        }

        var firstAffected = transactions.Min(t => t.DateUtc);
        _db.Transactions.RemoveRange(transactions);
        await StageHistoryInvalidationAsync(portfolio.Id, firstAffected, ct);
        await _db.SaveChangesAsync(ct);

        return TransactionResult.Ok(null);
    }

    public async Task<TransactionResult> MoveAssetAsync(
        Guid userId, string assetSlug, Guid fromPortfolioId, Guid toPortfolioId, CancellationToken ct = default)
    {
        if (fromPortfolioId == toPortfolioId)
        {
            return Invalid("Elige un portafolio de destino distinto del actual.");
        }

        // Los dos portafolios tienen que ser del usuario: un id ajeno es "no existe".
        var source = await FindPortfolioAsync(userId, fromPortfolioId, ct);
        var target = await FindPortfolioAsync(userId, toPortfolioId, ct);
        if (source is null || target is null)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "El portafolio no existe.");
        }

        var asset = await FindAssetAsync(NormalizeSlug(assetSlug), ct);
        if (asset is null)
        {
            return TransactionResult.Fail(TransactionFailure.AssetNotFound, "La moneda no existe.");
        }

        var moving = await _db.Transactions
            .Where(t => t.PortfolioId == source.Id && t.AssetId == asset.Id)
            .ToListAsync(ct);
        if (moving.Count == 0)
        {
            return TransactionResult.Fail(TransactionFailure.NotFound, "Esa moneda no está en el portafolio.");
        }

        // Si el destino ya tiene esa moneda, el historial junto tiene que cuadrar.
        var merged = (await LoadAssetTransactionsAsync(target.Id, asset.Id, ct)).Concat(moving).ToList();
        var problem = CheckHistory(asset, merged);
        if (problem is not null)
        {
            return Invalid("No se puede mover: " + problem);
        }

        var firstAffected = moving.Min(t => t.DateUtc);
        foreach (var transaction in moving)
        {
            transaction.PortfolioId = target.Id;
        }

        await StageHistoryInvalidationAsync(source.Id, firstAffected, ct);
        await StageHistoryInvalidationAsync(target.Id, firstAffected, ct);
        await _db.SaveChangesAsync(ct);

        return TransactionResult.Ok(null);
    }

    // ---------------------------------------------------------------- ayudas

    private static string NormalizeSlug(string? slug) => (slug ?? "").Trim().ToLowerInvariant();

    /// <summary>Descarta las fotos diarias del historial desde esa fecha; se guarda en el mismo SaveChanges que la transacción.</summary>
    private Task StageHistoryInvalidationAsync(Guid portfolioId, DateTime fromUtc, CancellationToken ct) =>
        _history is null ? Task.CompletedTask : _history.StageInvalidationAsync(portfolioId, fromUtc, ct);

    private static TransactionResult Invalid(params string[] errors) =>
        TransactionResult.Fail(TransactionFailure.Invalid, errors);

    private static PortfolioSummary EmptySummary(Guid? id, string name) =>
        new(id, name, 0m, 0m, 0m, null, 0m, 0m, 0m, null, 0, 0, "", Array.Empty<PositionDto>());

    /// <summary>
    /// El portafolio pedido si es del usuario (otro id, o de otro usuario, es null); sin id, el primero del usuario.
    /// </summary>
    private async Task<Portfolio?> FindPortfolioAsync(Guid userId, Guid? portfolioId, CancellationToken ct)
    {
        var query = _db.Portfolios.AsNoTracking().Where(p => p.UserId == userId);
        if (portfolioId is { } id)
        {
            return await query.FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        return await query.OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// El portafolio indicado (null si no existe o no es del usuario). Sin indicar, el primero; y si el usuario aún
    /// no tiene ninguno, se crea el de por defecto con su primera transacción.
    /// </summary>
    private async Task<Portfolio?> GetOrCreatePortfolioAsync(Guid userId, Guid? portfolioId, CancellationToken ct)
    {
        var existing = await FindPortfolioAsync(userId, portfolioId, ct);
        if (existing is not null || portfolioId is not null)
        {
            return existing;
        }

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = _options.DefaultPortfolioName,
            CreatedAtUtc = Now
        };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync(ct);
        return portfolio;
    }

    private Task<Asset?> FindAssetAsync(string slug, CancellationToken ct) =>
        _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);

    // Siempre filtra por el dueño: un id de otro usuario es "no existe".
    private Task<Transaction?> FindOwnedTransactionAsync(Guid userId, long id, CancellationToken ct) =>
        _db.Transactions.FirstOrDefaultAsync(
            t => t.Id == id && _db.Portfolios.Any(p => p.Id == t.PortfolioId && p.UserId == userId), ct);

    private Task<List<Transaction>> LoadAssetTransactionsAsync(Guid portfolioId, int assetId, CancellationToken ct) =>
        _db.Transactions.AsNoTracking()
            .Where(t => t.PortfolioId == portfolioId && t.AssetId == assetId)
            .ToListAsync(ct);

    private static Transaction Build(Guid portfolioId, int assetId, ValidatedTransaction v) => new()
    {
        PortfolioId = portfolioId,
        AssetId = assetId,
        Type = v.Type,
        Quantity = v.Quantity,
        UnitPriceUsd = v.UnitPriceUsd,
        FeeUsd = v.FeeUsd,
        DateUtc = v.DateUtc,
        Note = v.Note
    };

    /// <summary>null si el historial de la moneda cuadra; si no, el mensaje para el usuario.</summary>
    private string? CheckHistory(Asset asset, IReadOnlyList<Transaction> history)
    {
        try
        {
            _strategy.Compute(history);
            return null;
        }
        catch (InsufficientHoldingsException ex)
        {
            var missing = ex.Missing.ToString("0.############", CultureInfo.GetCultureInfo("es-ES"));
            var when = ex.Transaction.DateUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            return $"No tienes suficiente {asset.Symbol} el {when} UTC: faltan {missing} {asset.Symbol}.";
        }
    }

    private (decimal? Price, string? Source, DateTime? UpdatedAtUtc) ResolvePrice(Asset asset, DateTime now)
    {
        if (_cache.TryGet(asset.Id, out var live)
            && now - live.UpdatedAtUtc <= TimeSpan.FromSeconds(Math.Max(1, _live.StaleSeconds)))
        {
            return (live.PriceUsd, PriceSources.Live, live.UpdatedAtUtc);
        }

        return asset.Price is null
            ? (null, null, null)
            : (asset.Price.PriceUsd, PriceSources.Delayed, asset.Price.UpdatedAtUtc);
    }

    private static decimal? Percent(decimal? amount, decimal basis) =>
        amount is null || basis <= 0 ? null : amount / basis * 100m;

    private static TransactionDto ToDto(Transaction t, Asset a) => new(
        t.Id, a.Slug, a.Symbol, a.Name, a.ImageUrl, t.Type,
        t.Quantity, t.UnitPriceUsd, t.FeeUsd, t.DateUtc, t.Note);
}
