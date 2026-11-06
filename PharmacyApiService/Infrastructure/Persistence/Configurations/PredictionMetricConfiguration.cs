using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class PredictionMetricConfiguration : IEntityTypeConfiguration<PredictionMetric>
{
    public void Configure(EntityTypeBuilder<PredictionMetric> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName).HasMaxLength(255);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_prediction_metrics_scope_product",
            "(scope = 'Overall' AND product_name IS NULL) OR (scope = 'Product' AND product_name IS NOT NULL)"));
        builder.HasIndex(x => new { x.PredictionExecutionId, x.Method, x.Scope })
            .IsUnique().HasFilter("product_name IS NULL");
        builder.HasIndex(x => new { x.PredictionExecutionId, x.Method, x.Scope, x.ProductName })
            .IsUnique().HasFilter("product_name IS NOT NULL");

        builder.HasIndex(x => x.PredictionExecutionId);
    }
}
