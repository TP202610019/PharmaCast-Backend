using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.StoredFileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.BlobPath).HasMaxLength(500);
        builder.Property(x => x.BlobContainer).HasMaxLength(100);
        builder.Property(x => x.FileType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);
        builder.Property(x => x.DatasetStage)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(x => x.UploadedBy);
        builder.HasIndex(x => x.DatasetStage);
        builder.HasIndex(x => x.UploadStatus);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.MappingConfigurationId);
        builder.HasIndex(x => new { x.UploadedBy, x.CreatedAt });

        builder.HasMany(x => x.PredictionExecutions)
            .WithOne(x => x.Dataset)
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
