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

        builder.HasIndex(x => x.PredictionExecutionId);
    }
}
