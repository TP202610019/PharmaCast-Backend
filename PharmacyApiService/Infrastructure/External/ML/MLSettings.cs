namespace PharmacyApiService.Infrastructure.External.ML;

public class MLSettings
{
    public const string SectionName = "MLService";

    public string BaseUrl { get; init; } = string.Empty;
}
