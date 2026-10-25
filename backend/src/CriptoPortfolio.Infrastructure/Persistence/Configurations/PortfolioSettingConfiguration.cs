using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class PortfolioSettingConfiguration : IEntityTypeConfiguration<PortfolioSetting>
{
    public void Configure(EntityTypeBuilder<PortfolioSetting> b)
    {
        b.ToTable("PortfolioSettings");
        b.HasKey(x => x.PortfolioId);

        // Se borra con su portafolio.
        b.HasOne<Portfolio>()
            .WithOne()
            .HasForeignKey<PortfolioSetting>(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
