namespace PharmacyApiService.Infrastructure.External.Storage;

public class LocalStorageSettings
{
    public const string SectionName = "LocalStorage";

    public bool Enabled { get; init; }
    public string RootPath { get; init; } = "App_Data/storage";
}
