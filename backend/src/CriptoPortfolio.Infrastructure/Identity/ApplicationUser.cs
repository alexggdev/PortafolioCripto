using Microsoft.AspNetCore.Identity;

namespace CriptoPortfolio.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; }
}
