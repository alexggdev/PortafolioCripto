using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriptoPortfolio.Infrastructure.Persistence;

/// <summary>
/// Solo lo usan las herramientas (dotnet ef). Así crear migraciones no arranca el host
/// (que exigiría JWT y demás secretos configurados). Cadena: variable CRIPTO_CONNECTION o la de abajo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CRIPTO_CONNECTION")
            ?? "Server=localhost;Database=CriptoPortfolio;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
