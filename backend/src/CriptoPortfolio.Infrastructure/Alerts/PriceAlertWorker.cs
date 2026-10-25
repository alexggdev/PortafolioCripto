using CriptoPortfolio.Application.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Alerts;

/// <summary>Cada pocos segundos revisa las alertas activas contra los precios en vivo y dispara las que se cumplen.</summary>
public sealed class PriceAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PriceAlertOptions _options;
    private readonly ILogger<PriceAlertWorker> _log;

    public PriceAlertWorker(
        IServiceScopeFactory scopes,
        IOptions<PriceAlertOptions> options,
        ILogger<PriceAlertWorker> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);

            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var evaluator = scope.ServiceProvider.GetRequiredService<IPriceAlertEvaluator>();
                    await evaluator.EvaluateAsync(owner, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[WORKER] price-alerts: la pasada falló; se reintenta en el siguiente ciclo");
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
