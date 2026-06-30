using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class ForecastResultConfiguration : IEntityTypeConfiguration<ForecastResult>
{
    public void Configure(EntityTypeBuilder<ForecastResult> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ForecastDate).IsRequired();
        builder.Property(x => x.PredictedQuantity).IsRequired();

        builder.HasIndex(x => x.PredictionExecutionId);
        builder.HasIndex(x => new { x.PredictionExecutionId, x.ProductName });
        builder.HasIndex(x => new { x.PredictionExecutionId, x.ForecastDate });
    }
}
