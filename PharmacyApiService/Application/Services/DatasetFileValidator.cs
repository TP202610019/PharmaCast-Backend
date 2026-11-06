using Microsoft.AspNetCore.Http;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Application.Services;

public static class DatasetFileValidator
{
    public static string? GetConstraintError(IFormFile file)
    {
        if (file.Length == 0)
            return "File cannot be empty.";

        if (file.Length > AppConstants.MaxFileSizeBytes)
            return $"File exceeds the maximum allowed size of {AppConstants.MaxFileSizeBytes / (1024 * 1024)} MB.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AppConstants.AllowedFileExtensions.Dataset.Contains(ext))
            return $"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", AppConstants.AllowedFileExtensions.Dataset)}.";

        return null;
    }

    public static async Task<(int? RowCount, string? Error)> CheckStructureAsync(IFormFile file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext == ".csv")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var header = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(header))
                return (null, "CSV file has no header row.");

            int rows = 0;
            while (await reader.ReadLineAsync(ct) is string line)
                if (!string.IsNullOrWhiteSpace(line)) rows++;

            if (rows < 1)
                return (null, "CSV file must contain at least one data row.");

            return (rows, null);
        }

        if (ext == ".xlsx")
        {
            using var stream = file.OpenReadStream();
            var magic = new byte[2];
            if (await stream.ReadAsync(magic, ct) < 2 || magic[0] != 0x50 || magic[1] != 0x4B)
                return (null, "The file does not appear to be a valid XLSX file.");
        }

        if (ext == ".xls")
        {
            using var stream = file.OpenReadStream();
            var magic = new byte[4];
            if (await stream.ReadAsync(magic, ct) < 4
                || magic[0] != 0xD0 || magic[1] != 0xCF
                || magic[2] != 0x11 || magic[3] != 0xE0)
                return (null, "The file does not appear to be a valid XLS file.");
        }

        return (null, null);
    }
}
