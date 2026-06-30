using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class PredictionExecutionConfiguration : IEntityTypeConfiguration<PredictionExecution>
{
    public void Configure(EntityTypeBuilder<PredictionExecution> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ForecastPeriod).IsRequired();
        builder.Property(x => x.ExecutionStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(x => x.DatasetId);
        builder.HasIndex(x => x.ExecutedBy);
        builder.HasIndex(x => x.ExecutionStatus);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.ExecutedBy, x.CreatedAt });
        builder.HasIndex(x => new { x.DatasetId, x.ExecutionStatus });

        builder.HasOne(x => x.PurchasePlan)
            .WithOne(x => x.PredictionExecution)
            .HasForeignKey<PurchasePlan>(x => x.PredictionExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ForecastResults)
            .WithOne(x => x.PredictionExecution)
            .HasForeignKey(x => x.PredictionExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PredictionMetrics)
            .WithOne(x => x.PredictionExecution)
            .HasForeignKey(x => x.PredictionExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
