using CriptoPortfolio.Application.Auth;
using CriptoPortfolio.Infrastructure;
using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Identity;

/// <summary>Usa AddInfrastructureCore: la misma configuración de Identity y JWT que la API real, sobre SQLite.</summary>
public class AuthServiceTests : IDisposable
{
    private const string Password = "Prueba123";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:SigningKey"] = new string('k', 48),
                ["Jwt:ExpirationMinutes"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        services.AddInfrastructureCore(config);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    // Un scope por operación, como una petición HTTP distinta.
    private async Task<AuthResult> RegisterAsync(string email, string password = Password)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .RegisterAsync(new RegisterRequest { Email = email, Password = password });
    }

    private async Task<AuthResult> LoginAsync(string email, string password)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginRequest { Email = email, Password = password });
    }

    [Fact]
    public async Task Register_creates_the_user_and_returns_a_token()
    {
        var result = await RegisterAsync("ana@example.com");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.False(string.IsNullOrWhiteSpace(result.Response!.Token));
        Assert.Equal("ana@example.com", result.Response.Email);
        Assert.NotEqual(Guid.Empty, result.Response.UserId);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_one_clear_message()
    {
        Assert.True((await RegisterAsync("ana@example.com")).Succeeded);

        var again = await RegisterAsync("ANA@example.com"); // mayúsculas distintas: sigue siendo el mismo email

        Assert.False(again.Succeeded);
        Assert.Equal(AuthFailure.ValidationFailed, again.Failure);
        Assert.Single(again.Errors);
        Assert.Contains("ya está registrado", again.Errors[0]);
    }

    [Theory]
    [InlineData("corta1A")]        // menos de 8
    [InlineData("sinnumeros")]     // sin número ni mayúscula
    [InlineData("SINMINUSCULA1")]  // sin minúscula
    [InlineData("sinmayuscula1")]  // sin mayúscula
    public async Task Register_rejects_weak_passwords(string password)
    {
        var result = await RegisterAsync("weak@example.com", password);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthFailure.ValidationFailed, result.Failure);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_ignoring_email_case()
    {
        await RegisterAsync("ana@example.com");

        var result = await LoginAsync("ANA@Example.com", Password);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Response!.Token));
    }

    [Fact]
    public async Task Login_gives_the_same_error_for_wrong_password_and_unknown_email()
    {
        await RegisterAsync("ana@example.com");

        var wrongPassword = await LoginAsync("ana@example.com", "OtraClave123");
        var unknownEmail = await LoginAsync("nadie@example.com", Password);

        Assert.Equal(AuthFailure.InvalidCredentials, wrongPassword.Failure);
        Assert.Equal(AuthFailure.InvalidCredentials, unknownEmail.Failure);
        Assert.Equal(wrongPassword.Errors, unknownEmail.Errors); // no se revela qué emails existen
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_one()
    {
        await RegisterAsync("ana@example.com");

        AuthResult last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await LoginAsync("ana@example.com", "Incorrecta123");
        }

        Assert.Equal(AuthFailure.LockedOut, last.Failure); // el 5º intento ya bloquea

        var withRightPassword = await LoginAsync("ana@example.com", Password);
        Assert.False(withRightPassword.Succeeded);
        Assert.Equal(AuthFailure.LockedOut, withRightPassword.Failure);
    }

    [Fact]
    public async Task A_successful_login_resets_the_failed_attempts_counter()
    {
        await RegisterAsync("ana@example.com");

        for (var i = 0; i < 4; i++)
        {
            await LoginAsync("ana@example.com", "Incorrecta123");
        }

        Assert.True((await LoginAsync("ana@example.com", Password)).Succeeded); // resetea el contador

        // Si no se hubiera reseteado, este fallo sería el 5º y bloquearía.
        var afterReset = await LoginAsync("ana@example.com", "Incorrecta123");
        Assert.Equal(AuthFailure.InvalidCredentials, afterReset.Failure);
    }
}
