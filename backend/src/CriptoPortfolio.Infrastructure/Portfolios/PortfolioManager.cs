using System.Collections.Concurrent;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Portfolios;

public sealed class PortfolioManager : IPortfolioManager
{
    public const int MaxNameLength = 100;

    /// <summary>Imágenes disponibles (0 a 27). Las dibuja el cliente; aquí solo se valida el número.</summary>
    public const int AvatarCount = 28;

    // Un cambio a la vez por usuario: dos peticiones seguidas («Predeterminado» en dos portafolios) no se pisan y nunca
    // quedan dos predeterminados. Cada petición lee el estado después de que termine la anterior.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UserLocks = new();

    private readonly IAppDbContext _db;
    private readonly TimeProvider _time;
    private readonly PortfolioOptions _options;

    public PortfolioManager(IAppDbContext db, TimeProvider time, IOptions<PortfolioOptions> options)
    {
        _db = db;
        _time = time;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<PortfolioInfo>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var portfolios = await _db.Portfolios.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.Name, p.CreatedAtUtc })
            .ToListAsync(ct);

        var ids = portfolios.Select(p => p.Id).ToList();
        var settings = await _db.PortfolioSettings.AsNoTracking()
            .Where(s => ids.Contains(s.PortfolioId))
            .ToDictionaryAsync(s => s.PortfolioId, ct);
        var counts = await _db.Transactions.AsNoTracking()
            .Where(t => ids.Contains(t.PortfolioId))
            .GroupBy(t => t.PortfolioId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return portfolios
            .OrderBy(p => settings.TryGetValue(p.Id, out var s) ? s.SortOrder : int.MaxValue)
            .ThenBy(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Select(p => Info(p.Id, p.Name, p.CreatedAtUtc, counts.GetValueOrDefault(p.Id), settings.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<PortfolioResult> CreateAsync(Guid userId, string? name, int? avatar = null, CancellationToken ct = default)
    {
        var clean = Clean(name, out var error);
        if (error is not null)
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, error);
        }

        if (InvalidAvatar(avatar))
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, "La imagen elegida no existe.");
        }

        var count = await _db.Portfolios.CountAsync(p => p.UserId == userId, ct);
        if (count >= _options.MaxPortfoliosPerUser)
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid,
                $"Has alcanzado el máximo de {_options.MaxPortfoliosPerUser} portafolios.");
        }

        if (await NameTakenAsync(userId, clean, exceptId: null, ct))
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, "Ya tienes un portafolio con ese nombre.");
        }

        var portfolio = NewPortfolio(userId, clean);
        _db.Portfolios.Add(portfolio);
        var setting = new PortfolioSetting { PortfolioId = portfolio.Id, Avatar = avatar, SortOrder = count };
        _db.PortfolioSettings.Add(setting);
        await _db.SaveChangesAsync(ct);

        return PortfolioResult.Ok(Info(portfolio.Id, portfolio.Name, portfolio.CreatedAtUtc, 0, setting));
    }

    public Task<PortfolioResult> RenameAsync(Guid userId, Guid id, string? name, CancellationToken ct = default) =>
        UpdateAsync(userId, id, new PortfolioChanges(Name: name ?? ""), ct);

    public async Task<PortfolioResult> UpdateAsync(Guid userId, Guid id, PortfolioChanges changes, CancellationToken ct = default)
    {
        var gate = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await UpdateCoreAsync(userId, id, changes, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<PortfolioResult> UpdateCoreAsync(Guid userId, Guid id, PortfolioChanges changes, CancellationToken ct)
    {
        string? clean = null;
        if (changes.Name is not null)
        {
            clean = Clean(changes.Name, out var error);
            if (error is not null)
            {
                return PortfolioResult.Fail(PortfolioFailure.Invalid, error);
            }
        }

        if (InvalidAvatar(changes.Avatar))
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, "La imagen elegida no existe.");
        }

        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (portfolio is null)
        {
            return PortfolioResult.Fail(PortfolioFailure.NotFound, "El portafolio no existe.");
        }

        if (clean is not null)
        {
            if (await NameTakenAsync(userId, clean, exceptId: id, ct))
            {
                return PortfolioResult.Fail(PortfolioFailure.Invalid, "Ya tienes un portafolio con ese nombre.");
            }

            portfolio.Name = clean;
        }

        var setting = await SettingAsync(id, ct);
        if (changes.Avatar is { } avatar)
        {
            setting.Avatar = avatar;
        }

        if (changes.IncludeInTotal is { } include)
        {
            setting.IncludeInTotal = include;
        }

        if (changes.IsDefault is { } isDefault)
        {
            if (isDefault)
            {
                var others = await _db.PortfolioSettings
                    .Where(s => s.IsDefault && s.PortfolioId != id
                                && _db.Portfolios.Any(p => p.Id == s.PortfolioId && p.UserId == userId))
                    .ToListAsync(ct);
                foreach (var other in others)
                {
                    other.IsDefault = false;
                }
            }

            setting.IsDefault = isDefault;
        }

        await _db.SaveChangesAsync(ct);

        var count = await _db.Transactions.CountAsync(t => t.PortfolioId == id, ct);
        return PortfolioResult.Ok(Info(portfolio.Id, portfolio.Name, portfolio.CreatedAtUtc, count, setting));
    }

    public async Task<PortfolioResult> DuplicateAsync(Guid userId, Guid id, string? name, CancellationToken ct = default)
    {
        var clean = Clean(name, out var error);
        if (error is not null)
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, error);
        }

        var source = await _db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (source is null)
        {
            return PortfolioResult.Fail(PortfolioFailure.NotFound, "El portafolio no existe.");
        }

        var count = await _db.Portfolios.CountAsync(p => p.UserId == userId, ct);
        if (count >= _options.MaxPortfoliosPerUser)
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid,
                $"Has alcanzado el máximo de {_options.MaxPortfoliosPerUser} portafolios.");
        }

        if (await NameTakenAsync(userId, clean, exceptId: null, ct))
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, "Ya tienes un portafolio con ese nombre.");
        }

        var rows = await _db.Transactions.AsNoTracking().Where(t => t.PortfolioId == id).ToListAsync(ct);
        var total = await _db.Transactions.CountAsync(t => _db.Portfolios.Any(p => p.Id == t.PortfolioId && p.UserId == userId), ct);
        if (total + rows.Count > _options.MaxTransactionsPerUser)
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid,
                $"Duplicarlo superaría el máximo de {_options.MaxTransactionsPerUser} transacciones.");
        }

        var original = await _db.PortfolioSettings.AsNoTracking().FirstOrDefaultAsync(s => s.PortfolioId == id, ct);
        var copy = NewPortfolio(userId, clean);
        _db.Portfolios.Add(copy);
        var setting = new PortfolioSetting
        {
            PortfolioId = copy.Id,
            Avatar = original?.Avatar,
            IncludeInTotal = original?.IncludeInTotal ?? true,
            SortOrder = count
        };
        _db.PortfolioSettings.Add(setting);
        foreach (var t in rows)
        {
            _db.Transactions.Add(new Transaction
            {
                PortfolioId = copy.Id,
                AssetId = t.AssetId,
                Type = t.Type,
                Quantity = t.Quantity,
                UnitPriceUsd = t.UnitPriceUsd,
                FeeUsd = t.FeeUsd,
                DateUtc = t.DateUtc,
                Note = t.Note
            });
        }

        await _db.SaveChangesAsync(ct);
        return PortfolioResult.Ok(Info(copy.Id, copy.Name, copy.CreatedAtUtc, rows.Count, setting));
    }

    public async Task<PortfolioResult> ReorderAsync(Guid userId, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var owned = await _db.Portfolios.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count != owned.Count || ids.Distinct().Count() != ids.Count || !owned.ToHashSet().SetEquals(ids))
        {
            return PortfolioResult.Fail(PortfolioFailure.Invalid, "El orden debe incluir todos tus portafolios, cada uno una vez.");
        }

        var settings = await _db.PortfolioSettings.Where(s => owned.Contains(s.PortfolioId)).ToDictionaryAsync(s => s.PortfolioId, ct);
        for (var i = 0; i < ids.Count; i++)
        {
            if (!settings.TryGetValue(ids[i], out var setting))
            {
                setting = new PortfolioSetting { PortfolioId = ids[i] };
                _db.PortfolioSettings.Add(setting);
            }

            setting.SortOrder = i;
        }

        await _db.SaveChangesAsync(ct);
        return PortfolioResult.Ok(null);
    }

    public async Task<PortfolioResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var portfolio = await _db.Portfolios.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (portfolio is null)
        {
            return PortfolioResult.Fail(PortfolioFailure.NotFound, "El portafolio no existe.");
        }

        // Las transacciones, las fotos diarias y los ajustes se borran en cascada (clave foránea con DeleteBehavior.Cascade).
        _db.Portfolios.Remove(portfolio);
        await _db.SaveChangesAsync(ct);
        return PortfolioResult.Ok(null);
    }

    private Portfolio NewPortfolio(Guid userId, string name) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = name,
        CreatedAtUtc = _time.GetUtcNow().UtcDateTime
    };

    private static PortfolioInfo Info(Guid id, string name, DateTime createdAtUtc, int count, PortfolioSetting? setting) =>
        new(id, name, createdAtUtc, count, setting?.Avatar, setting?.IncludeInTotal ?? true, setting?.IsDefault ?? false);

    /// <summary>El ajuste del portafolio (seguido por el contexto); si no tenía, lo crea.</summary>
    private async Task<PortfolioSetting> SettingAsync(Guid portfolioId, CancellationToken ct)
    {
        var setting = await _db.PortfolioSettings.FirstOrDefaultAsync(s => s.PortfolioId == portfolioId, ct);
        if (setting is null)
        {
            setting = new PortfolioSetting { PortfolioId = portfolioId };
            _db.PortfolioSettings.Add(setting);
        }

        return setting;
    }

    private static bool InvalidAvatar(int? avatar) => avatar is < 0 or >= AvatarCount;

    private static string Clean(string? name, out string? error)
    {
        var clean = (name ?? "").Trim();
        error = null;
        if (clean.Length == 0)
        {
            error = "Escribe un nombre para el portafolio.";
        }
        else if (clean.Length > MaxNameLength)
        {
            error = $"El nombre admite como máximo {MaxNameLength} caracteres.";
        }

        return clean;
    }

    // La comparación sin distinguir mayúsculas la hace SQL Server (collation por defecto); en SQLite se hace aquí.
    private async Task<bool> NameTakenAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct)
    {
        var names = await _db.Portfolios.AsNoTracking()
            .Where(p => p.UserId == userId && (exceptId == null || p.Id != exceptId))
            .Select(p => p.Name)
            .ToListAsync(ct);
        return names.Any(n => string.Equals(n, name, StringComparison.CurrentCultureIgnoreCase));
    }
}
