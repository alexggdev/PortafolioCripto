using System.Globalization;
using CriptoPortfolio.Application.Alerts;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Alerts;

public sealed class PriceAlertService : IPriceAlertService
{
    // decimal(28,18): hasta 10 cifras enteras y 18 decimales.
    private const decimal MaxTargetUsd = 9_999_999_999m;
    private const int MaxListed = 500;

    private readonly IAppDbContext _db;
    private readonly AlertPriceReader _prices;
    private readonly TimeProvider _time;
    private readonly PriceAlertOptions _options;

    public PriceAlertService(
        IAppDbContext db,
        AlertPriceReader prices,
        TimeProvider time,
        IOptions<PriceAlertOptions> options)
    {
        _db = db;
        _prices = prices;
        _time = time;
        _options = options.Value;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<PriceAlertDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await (from a in _db.PriceAlerts.AsNoTracking()
                          join s in _db.Assets.AsNoTracking() on a.AssetId equals s.Id
                          where a.UserId == userId
                          orderby a.CreatedAtUtc descending, a.Id descending
                          select new { Alert = a, s.Slug, s.Symbol, s.Name, s.ImageUrl })
            .Take(MaxListed)
            .ToListAsync(ct);

        var activeAssetIds = rows.Where(r => r.Alert.TriggeredAtUtc == null).Select(r => r.Alert.AssetId).Distinct().ToList();
        var current = await _prices.GetPricesAsync(activeAssetIds, ct);

        return rows.Select(r => new PriceAlertDto(
            r.Alert.Id, r.Slug, r.Symbol, r.Name, r.ImageUrl,
            r.Alert.Direction, r.Alert.TargetPriceUsd,
            r.Alert.TriggeredAtUtc == null && current.TryGetValue(r.Alert.AssetId, out var price) ? price : null,
            r.Alert.CreatedAtUtc, r.Alert.TriggeredAtUtc, r.Alert.TriggeredPriceUsd,
            r.Alert.TriggeredAtUtc != null && r.Alert.ReadAtUtc == null)).ToList();
    }

    public async Task<AlertResult> CreateAsync(Guid userId, CreateAlertRequest request, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var slug = request.AssetSlug?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(slug) || slug.Length > 200)
        {
            errors.Add("Elige una moneda.");
        }

        if (!Enum.IsDefined(request.Direction))
        {
            errors.Add("Elige si quieres el aviso cuando suba o cuando baje.");
        }

        var target = request.TargetPriceUsd;
        if (target <= 0)
        {
            errors.Add("El precio objetivo debe ser mayor que cero.");
        }
        else if (target > MaxTargetUsd)
        {
            errors.Add("El precio objetivo es demasiado grande.");
        }
        else if (decimal.Round(target, 18) != target)
        {
            errors.Add("El precio objetivo admite como máximo 18 decimales.");
        }

        if (errors.Count > 0)
        {
            return AlertResult.Fail(AlertFailure.Invalid, errors.ToArray());
        }

        var asset = await _db.Assets.AsNoTracking()
            .Where(a => a.IsActive && a.Slug == slug)
            .Select(a => new { a.Id, a.Slug, a.Symbol, a.Name, a.ImageUrl })
            .FirstOrDefaultAsync(ct);
        if (asset is null)
        {
            return AlertResult.Fail(AlertFailure.NotFound, "La moneda no existe.");
        }

        var active = await _db.PriceAlerts.AsNoTracking()
            .Where(a => a.UserId == userId && a.TriggeredAtUtc == null)
            .Select(a => new { a.AssetId, a.Direction, a.TargetPriceUsd })
            .ToListAsync(ct);

        if (active.Count >= _options.MaxActivePerUser)
        {
            return AlertResult.Fail(
                AlertFailure.LimitReached, $"Solo puedes tener {_options.MaxActivePerUser} alertas activas. Borra alguna.");
        }

        if (active.Any(a => a.AssetId == asset.Id && a.Direction == request.Direction && a.TargetPriceUsd == target))
        {
            return AlertResult.Fail(AlertFailure.Invalid, "Ya tienes una alerta igual.");
        }

        var prices = await _prices.GetPricesAsync(new[] { asset.Id }, ct);
        if (!prices.TryGetValue(asset.Id, out var current))
        {
            return AlertResult.Fail(
                AlertFailure.Invalid, "Esta moneda no tiene un precio reciente todavía. Inténtalo en unos minutos.");
        }

        // Si ya se cumple, saltaría al instante: es casi seguro un error al escribir el precio.
        var met = request.Direction == AlertDirection.Above ? current >= target : current <= target;
        if (met)
        {
            var word = request.Direction == AlertDirection.Above ? "por encima" : "por debajo";
            return AlertResult.Fail(
                AlertFailure.Invalid,
                $"El precio actual ({current.ToString("0.########", CultureInfo.InvariantCulture)} USD) ya está {word} de ese objetivo.");
        }

        var alert = new PriceAlert
        {
            UserId = userId,
            AssetId = asset.Id,
            Direction = request.Direction,
            TargetPriceUsd = target,
            CreatedAtUtc = Now
        };
        _db.PriceAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);

        return AlertResult.Ok(new PriceAlertDto(
            alert.Id, asset.Slug, asset.Symbol, asset.Name, asset.ImageUrl,
            alert.Direction, alert.TargetPriceUsd, current, alert.CreatedAtUtc, null, null, false));
    }

    public async Task<bool> DeleteAsync(Guid userId, long id, CancellationToken ct = default) =>
        await _db.PriceAlerts.Where(a => a.Id == id && a.UserId == userId).ExecuteDeleteAsync(ct) > 0;

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        _db.PriceAlerts.AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.TriggeredAtUtc != null && a.ReadAtUtc == null, ct);

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _db.PriceAlerts
            .Where(a => a.UserId == userId && a.TriggeredAtUtc != null && a.ReadAtUtc == null)
            .ToListAsync(ct);
        if (unread.Count == 0)
        {
            return;
        }

        var now = Now;
        foreach (var alert in unread)
        {
            alert.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}
