using PharmacyApiService.Application.Interfaces;
using PharmacyApiService.Infrastructure.External.ML;
using PharmacyApiService.Infrastructure.External.Storage;

namespace PharmacyApiService.Infrastructure.External;

public static class ExternalServicesExtensions
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MLSettings>(config.GetSection(MLSettings.SectionName));
        services.Configure<BlobStorageSettings>(config.GetSection(BlobStorageSettings.SectionName));

        var mlSettings = config.GetSection(MLSettings.SectionName).Get<MLSettings>()
            ?? throw new InvalidOperationException("MLService settings section not found.");
        if (string.IsNullOrWhiteSpace(mlSettings.BaseUrl))
            throw new InvalidOperationException("MLService:BaseUrl is not configured.");

        services.AddHttpClient<IMLServiceClient, MLServiceClient>(client =>
        {
            client.BaseAddress = new Uri(mlSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(660);
        });

        var blobSettings = config.GetSection(BlobStorageSettings.SectionName).Get<BlobStorageSettings>();
        if (string.IsNullOrWhiteSpace(blobSettings?.ConnectionString))
            throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");

        services.AddScoped<IBlobStorageService, BlobStorageService>();

        return services;
    }
}
