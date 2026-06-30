namespace PharmacyApiService.Shared.Constants;

public static class AppConstants
{
    public static class Claims
    {
        public const string UserId = "uid";
        public const string Email = "email";
        public const string FullName = "fullName";
    }

    public static class Blob
    {
        public const string TemporaryDatasetContainer = "temporary-datasets";
        public const string RawDatasetContainer = "raw-datasets";

    }

    public static class AllowedFileExtensions
    {
        public static readonly string[] Dataset = [".csv", ".xls", ".xlsx"];
    }

    public const long MaxFileSizeBytes = 50 * 1024 * 1024;
}
