using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
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
        builder.Property(x => x.ArtifactPaths).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                v => new Dictionary<string, string>(v)));
        builder.Property(x => x.Warnings).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(new ValueComparer<List<string>?>(
                (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                v => v == null ? 0 : v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v == null ? null : v.ToList()));

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
