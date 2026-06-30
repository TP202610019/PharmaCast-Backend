using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class MappingConfigurationConfig : IEntityTypeConfiguration<MappingConfiguration>
{
    public void Configure(EntityTypeBuilder<MappingConfiguration> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConfigurationName).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => new { x.UserId, x.ConfigurationName }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.IsDefault });
    }
}
