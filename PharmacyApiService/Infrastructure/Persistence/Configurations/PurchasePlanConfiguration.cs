using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class PurchasePlanConfiguration : IEntityTypeConfiguration<PurchasePlan>
{
    public void Configure(EntityTypeBuilder<PurchasePlan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlanName).IsRequired().HasMaxLength(200);

        // One prediction execution can only produce one purchase plan
        builder.HasIndex(x => x.PredictionExecutionId).IsUnique();

        builder.HasMany(x => x.Items)
            .WithOne(x => x.PurchasePlan)
            .HasForeignKey(x => x.PurchasePlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
