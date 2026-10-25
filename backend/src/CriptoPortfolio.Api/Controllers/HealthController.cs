using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CriptoPortfolio.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>Estado de la API y de la conexión a SQL Server (para Docker y el despliegue).</summary>
    [HttpGet("/health")]
    public async Task<IActionResult> Get([FromServices] AppDbContext db, CancellationToken ct)
    {
        var dbUp = await db.Database.CanConnectAsync(ct);

        var body = new
        {
            status = dbUp ? "ok" : "degraded",
            db = dbUp ? "ok" : "down",
            utc = DateTime.UtcNow
        };

        return dbUp ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
