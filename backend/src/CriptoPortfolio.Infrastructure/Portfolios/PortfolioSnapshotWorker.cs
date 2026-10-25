using CriptoPortfolio.Application.Portfolios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Portfolios;

/// <summary>
/// Completa las fotos diarias del valor de cada portafolio (la gráfica lee de esa tabla). Corre cada pocos minutos:
/// casi siempre no hay nada que hacer; tras la medianoche UTC, en cuanto llegan los cierres de ayer, guarda el día nuevo.
/// </summary>
public sealed class PortfolioSnapshotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PortfolioHistoryOptions _options;
    private readonly ILogger<PortfolioSnapshotWorker> _log;

    public PortfolioSnapshotWorker(
        IServiceScopeFactory scopes,
        IOptions<PortfolioHistoryOptions> options,
        ILogger<PortfolioSnapshotWorker> log)
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
                    var service = scope.ServiceProvider.GetRequiredService<IPortfolioHistoryService>();
                    await service.SnapshotAllAsync(owner, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[WORKER] portfolio-snapshots: la pasada falló; se reintenta en el siguiente ciclo");
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
