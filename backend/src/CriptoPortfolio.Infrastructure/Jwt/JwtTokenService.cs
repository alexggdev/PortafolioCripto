using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CriptoPortfolio.Infrastructure.Jwt;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
    }

    public AuthToken CreateToken(Guid userId, string email)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.ExpirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AuthToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
