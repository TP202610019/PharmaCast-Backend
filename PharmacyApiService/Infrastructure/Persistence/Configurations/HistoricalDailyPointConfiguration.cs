using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class HistoricalDailyPointConfiguration : IEntityTypeConfiguration<HistoricalDailyPoint>
{
    public void Configure(EntityTypeBuilder<HistoricalDailyPoint> builder)
    {
        builder.ToTable("historical_sales_daily");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DatasetId).HasColumnName("dataset_id").IsRequired();
        builder.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder.HasOne(x => x.Dataset)
               .WithMany()
               .HasForeignKey(x => x.DatasetId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DatasetId, x.ProductName, x.Date }).IsUnique();
        builder.HasIndex(x => new { x.DatasetId, x.Date });
    }
}
