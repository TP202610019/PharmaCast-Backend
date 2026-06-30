using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Infrastructure.Repositories;

namespace PharmacyApiService.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IMappingFieldRepository, MappingFieldRepository>();
        services.AddScoped<IMappingConfigRepository, MappingConfigRepository>();
        services.AddScoped<IPredictionRepository, PredictionRepository>();
        services.AddScoped<IPurchasePlanRepository, PurchasePlanRepository>();

        return services;
    }
}
