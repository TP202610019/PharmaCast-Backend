using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class PurchasePlanItemConfiguration : IEntityTypeConfiguration<PurchasePlanItem>
{
    public void Configure(EntityTypeBuilder<PurchasePlanItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Supplier).HasMaxLength(200);

        builder.HasIndex(x => x.PurchasePlanId);
        builder.HasIndex(x => new { x.PurchasePlanId, x.ProductName });
    }
}
