using FluentValidation;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Application.Services;
using PharmacyApiService.Infrastructure.External;
using PharmacyApiService.Infrastructure.Persistence;
using PharmacyApiService.Infrastructure.Security;

namespace PharmacyApiService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddJwtSecurity(config);
        services.AddPersistence(config);
        services.AddExternalServices(config);
        services.AddApplicationServices();
        return services;
    }

    public static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDatasetService, DatasetService>();
        services.AddScoped<IMappingFieldService, MappingFieldService>();
        services.AddScoped<IMappingConfigService, MappingConfigService>();
        services.AddScoped<IPredictionService, PredictionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IPurchasePlanService, PurchasePlanService>();

        return services;
    }
}
