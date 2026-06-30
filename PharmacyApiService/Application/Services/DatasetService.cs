using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using PharmacyApiService.Application.Interfaces;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Domain.Enums;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Presentation.DTOs.Dataset;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Application.Services;

public class DatasetService : IDatasetService
{
    private readonly IDatasetRepository _datasets;
    private readonly IMappingConfigRepository _mappings;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<DatasetService> _logger;

    public DatasetService(
        IDatasetRepository datasets,
        IMappingConfigRepository mappings,
        IBlobStorageService blob,
        ILogger<DatasetService> logger)
    {
        _datasets = datasets;
        _mappings = mappings;
        _blob = blob;
        _logger = logger;
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task<DatasetResponse> UploadAsync(
        IFormFile file,
        Guid userId,
        Guid? mappingConfigId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[UPLOAD-START] UploadAsync — userId={UserId}, fileName={FileName}, size={Size}B, mappingConfigId={MappingId}",
            userId, file.FileName, file.Length, mappingConfigId);

        // 1. Basic constraints
        ValidateFileConstraints(file);
        _logger.LogInformation("[UPLOAD-1] File constraints OK — ext={Ext}, size={Size}B", Path.GetExtension(file.FileName), file.Length);

        // 2. Mapping config: existence + ownership + column existence check (if provided)
        if (mappingConfigId.HasValue)
        {
            _logger.LogInformation("[UPLOAD-2] Validating mapping config {MappingId}...", mappingConfigId.Value);
            var config = await _mappings.GetByIdWithFieldsAsync(mappingConfigId.Value, ct)
                ?? throw new NotFoundException(nameof(MappingConfiguration), mappingConfigId.Value);

            if (config.UserId != userId)
                throw new ForbiddenException();

            var headers = ExtractHeadersFromFormFile(file);
            ValidateMappedColumnsExist(headers, config);
            _logger.LogInformation("[UPLOAD-2] Mapping config validated OK");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileType = ExtToFileType(ext);
        var storedName = $"{Guid.NewGuid()}{ext}";
        var blobPath = $"{userId}/{storedName}";

        // 3. Save to temporary-datasets (stage = Temporary, status = Pending)
        _logger.LogInformation(
            "[UPLOAD-3] Saving to temporary storage — container={Container}, blobPath={BlobPath}",
            AppConstants.Blob.TemporaryDatasetContainer, blobPath);
        await UploadToContainerAsync(file, AppConstants.Blob.TemporaryDatasetContainer, blobPath, ct);

        var dataset = new Dataset
        {
            UploadedBy = userId,
            MappingConfigurationId = mappingConfigId,
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
            BlobPath = blobPath,
            BlobContainer = AppConstants.Blob.TemporaryDatasetContainer,
            FileType = fileType,
            DatasetStage = DatasetStage.Temporary,
            UploadStatus = UploadStatus.Pending
        };

        await _datasets.AddAsync(dataset, ct);
        await _datasets.SaveChangesAsync(ct);
        _logger.LogInformation("[UPLOAD-3] Dataset record persisted — datasetId={DatasetId}", dataset.Id);

        // 4. Structural validation — errors mark the record Failed and rethrow
        _logger.LogInformation("[UPLOAD-4] Running structural validation — fileType={FileType}...", fileType);
        int? rowCount = null;
        try
        {
            rowCount = await ValidateStructureAsync(file, ct);
            _logger.LogInformation("[UPLOAD-4] Structural validation OK — rowCount={RowCount}", rowCount);
        }
        catch (ValidationException vex)
        {
            _logger.LogWarning("[UPLOAD-4] Structural validation FAILED — {Error}", vex.Message);
            await MarkFailedAndCleanupAsync(dataset, blobPath, ct);
            throw;
        }

        // 5. Move to raw-datasets (stage = Raw, status = Completed)
        _logger.LogInformation(
            "[UPLOAD-5] Moving file to raw storage — from={From} to={To}",
            AppConstants.Blob.TemporaryDatasetContainer, AppConstants.Blob.RawDatasetContainer);
        try
        {
            await _blob.MoveAsync(
                AppConstants.Blob.TemporaryDatasetContainer, blobPath,
                AppConstants.Blob.RawDatasetContainer, blobPath,
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[UPLOAD-5] File move to raw storage FAILED");
            await MarkFailedAndCleanupAsync(dataset, blobPath, ct);
            throw new ExternalServiceException("Storage", "The file could not be moved to validated storage.");
        }

        dataset.DatasetStage = DatasetStage.Raw;
        dataset.UploadStatus = UploadStatus.Completed;
        dataset.BlobContainer = AppConstants.Blob.RawDatasetContainer;
        dataset.RowsCount = rowCount;
        dataset.UpdatedAt = DateTime.UtcNow;
        _datasets.Update(dataset);
        await _datasets.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[UPLOAD-DONE] Upload complete — datasetId={DatasetId}, stage=Raw, status=Completed, rowCount={RowCount}",
            dataset.Id, rowCount);

        return MapToResponse(dataset);
    }

    // ── Other operations ──────────────────────────────────────────────────────

    public async Task<DatasetResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Dataset), id);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        return MapToResponse(dataset);
    }

    public async Task<IReadOnlyList<DatasetResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var datasets = await _datasets.GetByUserAsync(userId, ct);
        return datasets.Select(MapToResponse).ToList();
    }

    public async Task<DatasetResponse> ValidateAsync(Guid datasetId, Guid userId, CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        // No-op if already validated; otherwise mark as processed
        if (dataset.DatasetStage < DatasetStage.Processed)
        {
            dataset.DatasetStage = DatasetStage.Processed;
            dataset.UpdatedAt = DateTime.UtcNow;
            _datasets.Update(dataset);
            await _datasets.SaveChangesAsync(ct);
        }

        return MapToResponse(dataset);
    }

    public async Task DeleteAsync(Guid datasetId, Guid userId, CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        if (dataset.BlobPath is not null && dataset.BlobContainer is not null)
        {
            try { await _blob.DeleteAsync(dataset.BlobContainer, dataset.BlobPath, ct); }
            catch { /* silently ignore — file may not exist */ }
        }

        _datasets.Remove(dataset);
        await _datasets.SaveChangesAsync(ct);
    }

    // ── Validation helpers ────────────────────────────────────────────────────

    private static void ValidateFileConstraints(IFormFile file)
    {
        if (file.Length == 0)
            throw new ValidationException("file", "File cannot be empty.");

        if (file.Length > AppConstants.MaxFileSizeBytes)
            throw new ValidationException("file",
                $"File exceeds the maximum allowed size of {AppConstants.MaxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AppConstants.AllowedFileExtensions.Dataset.Contains(ext))
            throw new ValidationException("file",
                $"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", AppConstants.AllowedFileExtensions.Dataset)}.");
    }

    /// <summary>
    /// Performs lightweight structural validation:
    /// - CSV: verifies a non-empty header row and at least one data row are present.
    /// - XLSX: verifies the ZIP magic bytes (PK header).
    /// - XLS: verifies the OLE2 compound-document magic bytes.
    /// Returns the data row count for CSV; null for Excel (row count delegated to ML Service).
    /// </summary>
    private static async Task<int?> ValidateStructureAsync(IFormFile file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext == ".csv")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var header = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(header))
                throw new ValidationException("file", "CSV file has no header row.");

            int rows = 0;
            while (await reader.ReadLineAsync(ct) is string line)
                if (!string.IsNullOrWhiteSpace(line)) rows++;

            if (rows < 1)
                throw new ValidationException("file", "CSV file must contain at least one data row.");

            return rows;
        }

        if (ext == ".xlsx")
        {
            // XLSX is a ZIP archive → magic bytes PK (0x50 0x4B)
            using var stream = file.OpenReadStream();
            var magic = new byte[2];
            if (await stream.ReadAsync(magic, ct) < 2 || magic[0] != 0x50 || magic[1] != 0x4B)
                throw new ValidationException("file", "The file does not appear to be a valid XLSX file.");
            return null;
        }

        if (ext == ".xls")
        {
            // XLS (OLE2 compound document) → magic bytes D0 CF 11 E0
            using var stream = file.OpenReadStream();
            var magic = new byte[4];
            if (await stream.ReadAsync(magic, ct) < 4
                || magic[0] != 0xD0 || magic[1] != 0xCF
                || magic[2] != 0x11 || magic[3] != 0xE0)
                throw new ValidationException("file", "The file does not appear to be a valid XLS file.");
            return null;
        }

        return null;
    }

    // ── Storage helpers ───────────────────────────────────────────────────────

    private async Task UploadToContainerAsync(IFormFile file, string container, string blobPath, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            await _blob.UploadAsync(stream, container, blobPath, file.ContentType ?? "application/octet-stream", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExternalServiceException("Storage", $"The file could not be saved to {container}.");
        }
    }

    private async Task MarkFailedAndCleanupAsync(Dataset dataset, string blobPath, CancellationToken ct)
    {
        dataset.UploadStatus = UploadStatus.Failed;
        dataset.UpdatedAt = DateTime.UtcNow;
        _datasets.Update(dataset);
        await _datasets.SaveChangesAsync(ct);

        try
        {
            await _blob.DeleteAsync(AppConstants.Blob.TemporaryDatasetContainer, blobPath, ct);
        }
        catch { /* best-effort cleanup */ }
    }

    // ── Header extraction ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ExtractHeadersAsync(Guid datasetId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("[HEADERS] ExtractHeadersAsync — datasetId={DatasetId}, userId={UserId}", datasetId, userId);

        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        if (dataset.BlobPath is null || dataset.BlobContainer is null)
        {
            _logger.LogWarning("[HEADERS] Dataset {DatasetId} has no blob path — returning empty headers", datasetId);
            return [];
        }

        var stream = await _blob.DownloadAsync(dataset.BlobContainer, dataset.BlobPath, ct);
        if (stream is null)
        {
            _logger.LogWarning("[HEADERS] Blob not found for dataset {DatasetId} — returning empty headers", datasetId);
            return [];
        }

        await using (stream)
        {
            var headers = ExtractHeadersFromStream(stream, dataset.FileType);
            _logger.LogInformation("[HEADERS] Extracted {Count} headers from dataset {DatasetId}: [{Headers}]",
                headers.Count, datasetId, string.Join(", ", headers));
            return headers;
        }
    }

    public async Task<Dictionary<string, double>> ReadInventoryStockAsync(
        Guid datasetId,
        Guid userId,
        string productColumn,
        string stockColumn,
        CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        if (dataset.BlobPath is null || dataset.BlobContainer is null)
            return [];

        var stream = await _blob.DownloadAsync(dataset.BlobContainer, dataset.BlobPath, ct);
        if (stream is null)
            return [];

        await using (stream)
            return ReadStockFromStream(stream, dataset.FileType, productColumn, stockColumn);
    }

    private static Dictionary<string, double> ReadStockFromStream(
        Stream stream, FileType fileType, string productColumn, string stockColumn)
    {
        return fileType == FileType.CSV
            ? ReadStockFromCsv(stream, productColumn, stockColumn)
            : ReadStockFromExcel(stream, productColumn, stockColumn);
    }

    private static Dictionary<string, double> ReadStockFromCsv(
        Stream stream, string productColumn, string stockColumn)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var headerLine = reader.ReadLine();
        if (headerLine is null) return result;

        if (headerLine.Length > 0 && headerLine[0] == '﻿')
            headerLine = headerLine[1..];

        var delimiter = DetectCsvDelimiter(headerLine);
        var headers = SplitCsvLine(headerLine, delimiter)
            .Select(h => h.Trim().Trim('"'))
            .ToArray();

        var productIdx = FindColumnIndex(headers, productColumn);
        var stockIdx   = FindColumnIndex(headers, stockColumn);
        if (productIdx < 0 || stockIdx < 0) return result;

        while (reader.ReadLine() is string line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = SplitCsvLine(line, delimiter);
            if (cells.Length <= Math.Max(productIdx, stockIdx)) continue;

            var name  = cells[productIdx].Trim().Trim('"');
            var rawQty = cells[stockIdx].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (double.TryParse(rawQty, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var qty))
                result[name] = qty;
        }

        return result;
    }

    private static Dictionary<string, double> ReadStockFromExcel(
        Stream stream, string productColumn, string stockColumn)
    {
        var config = new ExcelReaderConfiguration
        {
            FallbackEncoding = System.Text.Encoding.GetEncoding(1252),
            LeaveOpen = false,
        };

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        using var excelReader = ExcelReaderFactory.CreateReader(stream, config);

        do
        {
            int colCount = excelReader.FieldCount;
            int productIdx = -1, stockIdx = -1;
            bool headerFound = false;

            while (excelReader.Read())
            {
                if (!headerFound)
                {
                    // Find header row
                    var rowHeaders = Enumerable.Range(0, colCount)
                        .Select(i => excelReader.GetValue(i)?.ToString()?.Trim() ?? "")
                        .ToArray();

                    productIdx = FindColumnIndex(rowHeaders, productColumn);
                    stockIdx   = FindColumnIndex(rowHeaders, stockColumn);

                    if (productIdx >= 0 && stockIdx >= 0)
                        headerFound = true;
                    continue;
                }

                var name   = excelReader.GetValue(productIdx)?.ToString()?.Trim();
                var rawQty = excelReader.GetValue(stockIdx)?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(name)) continue;
                if (double.TryParse(rawQty, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var qty))
                    result[name] = qty;
            }

            if (result.Count > 0) break;
        }
        while (excelReader.NextResult());

        return result;
    }

    private static int FindColumnIndex(string[] headers, string columnName)
    {
        var target = columnName.ToLowerInvariant().Trim();
        for (int i = 0; i < headers.Length; i++)
            if (headers[i].ToLowerInvariant().Trim() == target)
                return i;
        return -1;
    }

    public IReadOnlyList<string> ParseFileHeaders(IFormFile file) => ExtractHeadersFromFormFile(file);

    private static IReadOnlyList<string> ExtractHeadersFromFormFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileType = ExtToFileType(ext);
        using var stream = file.OpenReadStream();
        return ExtractHeadersFromStream(stream, fileType);
    }

    private static IReadOnlyList<string> ExtractHeadersFromStream(Stream stream, FileType fileType)
    {
        if (fileType == FileType.CSV)
            return ExtractCsvHeaders(stream);

        return ExtractExcelHeaders(stream);
    }

    private static IReadOnlyList<string> ExtractCsvHeaders(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);

        // Skip blank rows before the first content row
        string? firstLine = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                firstLine = line;
                break;
            }
        }

        if (firstLine is null)
            throw new ValidationException("file", "El archivo CSV no contiene encabezados.");

        // Strip UTF-8 BOM (U+FEFF) if present — StreamReader without explicit encoding may leave it
        if (firstLine.Length > 0 && firstLine[0] == '﻿')
            firstLine = firstLine[1..];

        var delimiter = DetectCsvDelimiter(firstLine);
        var rawHeaders = SplitCsvLine(firstLine, delimiter);
        return ValidateAndNormalizeHeaders(rawHeaders);
    }

    private static char DetectCsvDelimiter(string line)
    {
        // Count unquoted occurrences of each candidate; pick the most frequent
        var candidates = new[] { ',', ';', '\t' };
        var best = candidates
            .Select(d => (delimiter: d, count: CountUnquotedOccurrences(line, d)))
            .Where(x => x.count > 0)
            .OrderByDescending(x => x.count)
            .Select(x => (char?)x.delimiter)
            .FirstOrDefault();
        return best ?? ',';
    }

    private static int CountUnquotedOccurrences(string line, char ch)
    {
        int count = 0;
        bool inQuote = false;
        foreach (char c in line)
        {
            if (c == '"') inQuote = !inQuote;
            else if (!inQuote && c == ch) count++;
        }
        return count;
    }

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuote = !inQuote;
                }
            }
            else if (c == delimiter && !inQuote)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString().Trim());
        return [.. fields];
    }

    private static IReadOnlyList<string> ExtractExcelHeaders(Stream stream)
    {
        // FallbackEncoding handles legacy XLS files that omit encoding metadata (common with CP1252/Latin-1)
        var config = new ExcelReaderConfiguration
        {
            FallbackEncoding = System.Text.Encoding.GetEncoding(1252),
            LeaveOpen = false,
        };

        using var excelReader = ExcelReaderFactory.CreateReader(stream, config);

        // Iterate worksheets until one with enough structured content is found
        do
        {
            int sheetColumnCount = excelReader.FieldCount;

            while (excelReader.Read())
            {
                // Count non-empty cells in this row
                int nonEmpty = 0;
                for (int i = 0; i < sheetColumnCount; i++)
                {
                    if (!string.IsNullOrWhiteSpace(excelReader.GetValue(i)?.ToString()))
                        nonEmpty++;
                }

                // Skip fully empty rows and single-cell rows when the sheet has multiple columns.
                // A single occupied cell in a multi-column sheet is almost always a report title
                // ("REPORTE DE VENTAS - ABRIL 2026"), not a header row.
                int minRequired = sheetColumnCount > 1 ? 2 : 1;
                if (nonEmpty < minRequired) continue;

                // First row that passes the threshold is the header row
                var raw = new List<string>();
                for (int i = 0; i < sheetColumnCount; i++)
                    raw.Add(excelReader.GetValue(i)?.ToString()?.Trim() ?? "");

                return ValidateAndNormalizeHeaders(raw);
            }
        }
        while (excelReader.NextResult());

        throw new ValidationException("file", "El archivo Excel no contiene encabezados válidos en ninguna hoja.");
    }

    private static IReadOnlyList<string> ValidateAndNormalizeHeaders(IEnumerable<string> rawHeaders)
    {
        var headers = rawHeaders.Select(h => h.Trim().Trim('"')).ToList();

        // Strip BOM from first header
        if (headers.Count > 0 && headers[0].StartsWith('﻿'))
            headers[0] = headers[0].TrimStart('﻿');

        // Drop trailing empty columns (Excel often exports empty trailing cells)
        while (headers.Count > 0 && string.IsNullOrWhiteSpace(headers[^1]))
            headers.RemoveAt(headers.Count - 1);

        if (headers.Count == 0)
            throw new ValidationException("file", "No se encontraron encabezados en el archivo.");

        // Detect duplicate headers (case-insensitive)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = headers
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Where(h => !seen.Add(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicates.Count > 0)
            throw new ValidationException("file",
                $"El archivo contiene encabezados duplicados: {string.Join(", ", duplicates)}.");

        var valid = headers.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (valid.Count == 0)
            throw new ValidationException("file", "No se encontraron encabezados no vacíos en el archivo.");

        return valid;
    }

    private static void ValidateMappedColumnsExist(IReadOnlyList<string> headers, MappingConfiguration mapping)
    {
        var normalized = headers.Select(h => h.ToLowerInvariant()).ToHashSet();
        var missing = mapping.Fields
            .Where(f => f.MappingField?.IsRequired == true
                        && !normalized.Contains(f.DatasetColumnName.ToLowerInvariant()))
            .Select(f => $"'{f.DatasetColumnName}' ({f.MappingField!.DisplayName})")
            .ToList();

        if (missing.Count > 0)
            throw new ValidationException("mapping",
                $"Las siguientes columnas del mapeo no existen en el archivo: {string.Join(", ", missing)}.");
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static FileType ExtToFileType(string ext) => ext switch
    {
        ".csv"  => FileType.CSV,
        ".xls"  => FileType.XLS,
        ".xlsx" => FileType.XLSX,
        _       => throw new ValidationException("file", "Unsupported file type.")
    };

    private static DatasetResponse MapToResponse(Dataset d) => new()
    {
        Id = d.Id,
        OriginalFileName = d.OriginalFileName,
        StoredFileName = d.StoredFileName,
        BlobPath = d.BlobPath,
        FileType = d.FileType,
        DatasetStage = d.DatasetStage,
        UploadStatus = d.UploadStatus,
        RowsCount = d.RowsCount,
        MappingConfigurationId = d.MappingConfigurationId,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
