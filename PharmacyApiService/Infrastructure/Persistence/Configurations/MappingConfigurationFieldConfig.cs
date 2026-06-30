using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class MappingConfigurationFieldConfig : IEntityTypeConfiguration<MappingConfigurationField>
{
    public void Configure(EntityTypeBuilder<MappingConfigurationField> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DatasetColumnName).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => new { x.MappingConfigurationId, x.MappingFieldId }).IsUnique();
        builder.HasIndex(x => x.MappingConfigurationId);

        builder.HasOne(x => x.MappingConfiguration)
            .WithMany(m => m.Fields)
            .HasForeignKey(x => x.MappingConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MappingField)
            .WithMany(f => f.MappingConfigurationFields)
            .HasForeignKey(x => x.MappingFieldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
