using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> b)
    {
        b.ToTable("SyncStates");
        b.HasKey(x => x.Name);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.LeaseOwner).HasMaxLength(200);
        b.Property(x => x.LastError).HasMaxLength(2000);
    }
}
