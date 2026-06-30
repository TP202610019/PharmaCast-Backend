namespace PharmacyApiService.Infrastructure.External.Storage;

public class BlobStorageSettings
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
}
