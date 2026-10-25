using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CriptoPortfolio.Infrastructure.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Jwt;

public class JwtTokenServiceTests
{
    private static readonly string Key = new('k', 48);

    private static JwtTokenService Create(FakeTimeProvider time, string key, int minutes = 30) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "iss",
            Audience = "aud",
            SigningKey = key,
            ExpirationMinutes = minutes
        }), time);

    private static FakeTimeProvider Clock() =>
        new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Token_contains_subject_email_issuer_audience_and_expiry()
    {
        var time = Clock();
        var userId = Guid.NewGuid();

        var result = Create(time, Key).CreateToken(userId, "ana@example.com");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("ana@example.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("iss", jwt.Issuer);
        Assert.Contains("aud", jwt.Audiences);
        Assert.Equal(time.GetUtcNow().UtcDateTime.AddMinutes(30), result.ExpiresAtUtc);
        Assert.Equal(result.ExpiresAtUtc, jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Each_token_has_a_unique_id()
    {
        var service = Create(Clock(), Key);
        var id = Guid.NewGuid();

        var a = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(id, "a@b.com").Token);
        var b = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(id, "a@b.com").Token);

        Assert.NotEqual(a.Id, b.Id);
    }

    private static TokenValidationParameters Parameters(string key) => new()
    {
        ValidIssuer = "iss",
        ValidAudience = "aud",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuerSigningKey = true,
        ValidateLifetime = false // el reloj del test es falso; aquí solo se comprueba la firma
    };

    [Fact]
    public void Token_validates_with_the_same_key()
    {
        var token = Create(Clock(), Key).CreateToken(Guid.NewGuid(), "ana@example.com").Token;

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, Parameters(Key), out var validated);

        Assert.NotNull(principal);
        Assert.NotNull(validated);
    }

    [Fact]
    public void Token_is_rejected_with_a_different_key()
    {
        var token = Create(Clock(), Key).CreateToken(Guid.NewGuid(), "ana@example.com").Token;

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, Parameters(new string('z', 48)), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("demasiado-corta")]
    public void Options_reject_missing_or_short_signing_key(string key)
    {
        var options = new JwtOptions { SigningKey = key };

        Assert.False(options.IsValid(out var error));
        Assert.Contains("SigningKey", error);
        Assert.Throws<InvalidOperationException>(options.EnsureValid);
    }

    [Fact]
    public void Options_accept_a_long_enough_key()
    {
        Assert.True(new JwtOptions { SigningKey = Key }.IsValid(out var error));
        Assert.Null(error);
    }
}
