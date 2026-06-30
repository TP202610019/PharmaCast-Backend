using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasMany(x => x.Sessions)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Datasets)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.MappingConfigurations)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PredictionExecutions)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.ExecutedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
