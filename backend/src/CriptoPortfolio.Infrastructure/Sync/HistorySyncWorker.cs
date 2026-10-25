using CriptoPortfolio.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Sync;

/// <summary>Cada pocos minutos completa el histórico diario que falte. Casi siempre no hay nada que hacer.</summary>
public sealed class HistorySyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly HistorySyncOptions _options;
    private readonly ILogger<HistorySyncWorker> _log;

    public HistorySyncWorker(
        IServiceScopeFactory scopes,
        IOptions<HistorySyncOptions> options,
        ILogger<HistorySyncWorker> log)
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
                try
                {
                    using var scope = _scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IHistorySyncService>();
                    await service.SyncAsync(owner, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[WORKER] daily-history: la pasada falló; se reintenta en el siguiente ciclo");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }
}
