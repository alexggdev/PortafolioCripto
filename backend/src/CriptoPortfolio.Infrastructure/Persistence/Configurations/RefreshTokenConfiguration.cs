using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);

        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);

        // Al borrar el usuario (RGPD) desaparecen sus sesiones.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
