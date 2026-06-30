using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence.Exceptions;

namespace PharmacyApiService.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    // Value converters declared once — shared across all DateTime and DateTime? properties
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
        new(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> UtcNullableDateTimeConverter =
        new(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : null,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<MappingField> MappingFields => Set<MappingField>();
    public DbSet<MappingConfiguration> MappingConfigurations => Set<MappingConfiguration>();
    public DbSet<MappingConfigurationField> MappingConfigurationFields => Set<MappingConfigurationField>();
    public DbSet<PredictionExecution> PredictionExecutions => Set<PredictionExecution>();
    public DbSet<ForecastResult> ForecastResults => Set<ForecastResult>();
    public DbSet<PredictionMetric> PredictionMetrics => Set<PredictionMetric>();
    public DbSet<PurchasePlan> PurchasePlans => Set<PurchasePlan>();
    public DbSet<PurchasePlanItem> PurchasePlanItems => Set<PurchasePlanItem>();
    public DbSet<PlanEvaluation> PlanEvaluations => Set<PlanEvaluation>();
    public DbSet<PlanEvaluationItem> PlanEvaluationItems => Set<PlanEvaluationItem>();
    public DbSet<HistoricalDailyPoint> HistoricalDailyPoints => Set<HistoricalDailyPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global UTC normalization: every DateTime/DateTime? column mapped to 'timestamp with time zone'
        // must carry Kind=Utc — Npgsql 9.x enforces this strictly. This converter catches any value
        // that slips through without an explicit DateTimeKind.Utc (e.g. deserialized ML response dates).
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.GetValueConverter() is null))
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(UtcDateTimeConverter);
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(UtcNullableDateTimeConverter);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw DatabaseExceptionHandler.Translate(ex);
        }
    }
}
