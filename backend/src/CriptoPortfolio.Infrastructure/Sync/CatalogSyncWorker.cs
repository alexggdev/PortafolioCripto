using CriptoPortfolio.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Sync;

/// <summary>Lanza la sincronización del catálogo cada IntervalMinutes. El candado en SQL evita solapes entre instancias.</summary>
public sealed class CatalogSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CatalogSyncOptions _options;
    private readonly ILogger<CatalogSyncWorker> _log;

    public CatalogSyncWorker(
        IServiceScopeFactory scopes,
        IOptions<CatalogSyncOptions> options,
        ILogger<CatalogSyncWorker> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);

            using var timer = new PeriodicTimer(interval);
            do
            {
                await RunOnceAsync(owner, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }

    private async Task RunOnceAsync(string owner, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
            var result = await service.SyncAsync(owner, ct);

            if (!result.Executed)
            {
                _log.LogDebug("[WORKER] catalog: otra instancia tiene el candado, se omite");
            }
            else if (result.Error is not null)
            {
                _log.LogWarning("[WORKER] catalog: terminó con error: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Un fallo no debe matar el worker: se reintenta en el siguiente ciclo.
            _log.LogError(ex, "[WORKER] catalog: error inesperado");
        }
    }
}
