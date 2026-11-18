using Microsoft.AspNetCore.Http;
using PharmacyApiService.Shared.Constants;
using ExcelDataReader;

namespace PharmacyApiService.Application.Services;

public static class DatasetFileValidator
{
    public static string? GetConstraintError(IFormFile file)
    {
        if (file.Length == 0)
            return "El archivo está vacío.";

        if (file.Length > AppConstants.MaxFileSizeBytes)
            return $"El archivo supera el tamaño máximo de {AppConstants.MaxFileSizeBytes / (1024 * 1024)} MB.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AppConstants.AllowedFileExtensions.Dataset.Contains(ext))
            return $"El formato '{ext}' no está permitido. Formatos aceptados: {string.Join(", ", AppConstants.AllowedFileExtensions.Dataset)}.";

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
                return (null, "El archivo CSV no contiene una fila de encabezados.");

            int rows = 0;
            while (await reader.ReadLineAsync(ct) is string line)
                if (!string.IsNullOrWhiteSpace(line)) rows++;

            if (rows < 1)
                return (null, "El archivo CSV debe contener al menos una fila de datos.");

            return (rows, null);
        }

        if (ext == ".xlsx")
        {
            using var stream = file.OpenReadStream();
            var magic = new byte[2];
            if (await stream.ReadAsync(magic, ct) < 2 || magic[0] != 0x50 || magic[1] != 0x4B)
                return (null, "El archivo no tiene un formato XLSX válido.");
        }

        if (ext == ".xls")
        {
            using var stream = file.OpenReadStream();
            var magic = new byte[4];
            if (await stream.ReadAsync(magic, ct) < 4
                || magic[0] != 0xD0 || magic[1] != 0xCF
                || magic[2] != 0x11 || magic[3] != 0xE0)
                return (null, "El archivo no tiene un formato XLS válido.");
        }

        // Inspect the workbook itself: a valid file signature alone does not
        // guarantee headers or data in the first sheet consumed by the pipeline.
        if (ext is ".xls" or ".xlsx")
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            try
            {
                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                if (!reader.Read() || !Enumerable.Range(0, reader.FieldCount)
                    .Any(i => !string.IsNullOrWhiteSpace(reader.GetValue(i)?.ToString())))
                    return (null, "El archivo Excel no contiene una fila de encabezados.");
                int rows = 0;
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (Enumerable.Range(0, reader.FieldCount)
                        .Any(i => !string.IsNullOrWhiteSpace(reader.GetValue(i)?.ToString()))) rows++;
                }
                return rows == 0
                    ? (null, "El archivo Excel debe contener al menos una fila de datos.")
                    : (rows, null);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is ExcelDataReader.Exceptions.ExcelReaderException
                or System.IO.InvalidDataException or System.Xml.XmlException)
            {
                return (null, "No se pudo leer el archivo Excel. Verifica que sea un XLS o XLSX válido y sin contraseña.");
            }
        }

        return (null, null);
    }
}
