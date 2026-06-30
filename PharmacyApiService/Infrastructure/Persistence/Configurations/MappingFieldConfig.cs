using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class MappingFieldConfig : IEntityTypeConfiguration<MappingField>
{
    public void Configure(EntityTypeBuilder<MappingField> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FieldKey).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.FieldType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.FieldKey).IsUnique();
        builder.HasIndex(x => x.FieldType);
        builder.HasIndex(x => x.IsActive);
    }
}
