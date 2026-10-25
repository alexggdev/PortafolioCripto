using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriptoPortfolio.Tests.Infrastructure.Support;

/// <summary>
/// Base de datos SQLite en memoria con el esquema real (EnsureCreated). La conexión se mantiene
/// abierta porque la base en memoria desaparece al cerrarla. Cada test crea la suya.
/// Limitación: SQLite no prueba el comportamiento concurrente real de SQL Server.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteConnection Connection => _connection;
    public DbContextOptions<AppDbContext> Options { get; }

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(Options);
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
