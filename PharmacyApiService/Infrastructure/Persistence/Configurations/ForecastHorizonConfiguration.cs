using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class ForecastHorizonConfiguration : IEntityTypeConfiguration<ForecastHorizon>
{
    public void Configure(EntityTypeBuilder<ForecastHorizon> builder)
    {
        builder.HasKey(x => x.Days);
        builder.Property(x => x.Days).ValueGeneratedNever();
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.HasData(
            new ForecastHorizon { Days = 30, DisplayName = "30 días", IsActive = true },
            new ForecastHorizon { Days = 60, DisplayName = "60 días", IsActive = true },
            new ForecastHorizon { Days = 90, DisplayName = "90 días", IsActive = true });
    }
}
