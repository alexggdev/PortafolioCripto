using CriptoPortfolio.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Fx;

/// <summary>
/// Mantiene al día los tipos de cambio en memoria. Cada instancia del backend tiene su propia caché y la
/// llamada es ligera (una por pasada), así que no usa candado en SQL.
/// </summary>
public sealed class FxRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly FxOptions _options;
    private readonly ILogger<FxRefreshWorker> _log;

    public FxRefreshWorker(IServiceScopeFactory scopes, IOptions<FxOptions> options, ILogger<FxRefreshWorker> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refresh = TimeSpan.FromMinutes(Math.Max(5, _options.RefreshMinutes));
        var retry = TimeSpan.FromMinutes(Math.Max(1, _options.RetryMinutes));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopes.CreateScope())
                {
                    var service = scope.ServiceProvider.GetRequiredService<IFxRefreshService>();
                    var result = await service.RefreshAsync(stoppingToken);
                    if (result.Updated)
                    {
                        _log.LogInformation("[WORKER] fx-rates: tipos de cambio actualizados");
                    }

                    await Task.Delay(result.Updated ? refresh : retry, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }
}
