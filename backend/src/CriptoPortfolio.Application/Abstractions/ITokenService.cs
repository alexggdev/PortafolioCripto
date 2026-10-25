using CriptoPortfolio.Application.Auth;

namespace CriptoPortfolio.Application.Abstractions;

public interface ITokenService
{
    AuthToken CreateToken(Guid userId, string email);
}
