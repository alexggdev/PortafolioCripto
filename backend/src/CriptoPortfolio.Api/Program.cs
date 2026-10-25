using CriptoPortfolio.Api.Extensions;
using CriptoPortfolio.Infrastructure;
using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

// Logger mínimo para los errores de arranque (antes de leer la configuración).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration, corsOrigins);

    // Detrás de Next.js la IP que ve la API es la del servidor de Next. Next reenvía la del visitante en
    // X-Forwarded-For y aquí solo se acepta si la petición viene de localhost (proxy de confianza por defecto).
    // Si Next corre en otra máquina, hay que añadir su IP a KnownProxies (Etapa 8).
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    });

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("[DB] Aplicando migraciones pendientes...");
        await db.Database.MigrateAsync();
    }

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    // Si el navegador cancela la petición (el usuario navega o pulsa «atrás»), SQL Server lanza una
    // SqlException en vez de OperationCanceledException. No es un fallo: se responde 499 sin registrar un error.
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (corsOrigins.Length > 0)
    {
        app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
    }

    // La autenticación va antes del limitador para que la zona privada se limite por usuario y no por IP.
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "[API] La aplicación terminó de forma inesperada");
    return 1; // código de salida distinto de 0: Docker, systemd y CI deben ver el fallo
}
finally
{
    Log.CloseAndFlush();
}

// Necesario para los tests de integración (WebApplicationFactory<Program>) en etapas posteriores.
public partial class Program
{
}
