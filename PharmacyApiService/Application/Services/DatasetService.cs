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

    public async Task<DatasetUploadResult> UploadAsync(
        IFormFile file,
        Guid userId,
        Guid? mappingConfigId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[UPLOAD-START] UploadAsync — userId={UserId}, fileName={FileName}, size={Size}B, mappingConfigId={MappingId}",
            userId, file.FileName, file.Length, mappingConfigId);

        // Invalid input is a normal API result, not an exception for the debugger.
        var fileError = DatasetFileValidator.GetConstraintError(file);
        if (fileError is not null)
            return new DatasetUploadResult(null, fileError);

        var (rowCount, structureError) = await DatasetFileValidator.CheckStructureAsync(file, ct);
        if (structureError is not null)
            return new DatasetUploadResult(null, structureError);

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
            UploadStatus = UploadStatus.Pending,
            RowsCount = rowCount
        };

        try
        {
            await _datasets.AddAsync(dataset, ct);
            await _datasets.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await _blob.DeleteAsync(AppConstants.Blob.TemporaryDatasetContainer,
                    blobPath, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                _logger.LogWarning(cleanupError, "Could not compensate temporary upload {BlobPath}", blobPath);
            }
            throw;
        }
        _logger.LogInformation("[UPLOAD-3] Dataset record persisted — datasetId={DatasetId}", dataset.Id);

        return new DatasetUploadResult(MapToResponse(dataset), null);
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

        if (!dataset.MappingConfigurationId.HasValue)
            throw new ValidationException("mappingConfigurationId", "Confirma el dataset con una configuración de mapeo.");
        return await ConfirmAsync(datasetId, userId,
            new ConfirmDatasetRequest { MappingConfigurationId = dataset.MappingConfigurationId.Value }, ct);
    }

    public async Task<DatasetResponse> ConfirmAsync(
        Guid datasetId, Guid userId, ConfirmDatasetRequest request, CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);
        if (dataset.UploadedBy != userId) throw new ForbiddenException();
        if (request.FieldType is not ("sales" or "inventory"))
            throw new ValidationException("fieldType", "Usa sales o inventory.");
        var mapping = await _mappings.GetByIdWithFieldsAsync(request.MappingConfigurationId, ct)
            ?? throw new NotFoundException(nameof(MappingConfiguration), request.MappingConfigurationId);
        if (mapping.UserId != userId) throw new ForbiddenException();

        var headers = await ExtractHeadersAsync(datasetId, userId, ct);
        ValidateConfirmationMapping(headers, mapping, request.FieldType);
        if (dataset.DatasetStage == DatasetStage.Raw && dataset.UploadStatus == UploadStatus.Completed)
        {
            if (dataset.MappingConfigurationId != mapping.Id)
                throw new ConflictException("El dataset ya fue confirmado con otro mapeo.");
            return MapToResponse(dataset);
        }
        if (dataset.DatasetStage != DatasetStage.Temporary || dataset.UploadStatus != UploadStatus.Pending)
            throw new ValidationException("dataset", "Solo se pueden confirmar datasets Temporary/Pending.");
        if (dataset.BlobContainer != AppConstants.Blob.TemporaryDatasetContainer || dataset.BlobPath is null)
            throw new ValidationException("dataset", "El dataset no tiene un archivo temporal válido.");

        var source = dataset.BlobContainer;
        var destination = AppConstants.Blob.RawDatasetContainer;
        await _blob.MoveAsync(source, dataset.BlobPath, destination, dataset.BlobPath, ct);
        try
        {
            dataset.MappingConfigurationId = mapping.Id;
            dataset.DatasetStage = DatasetStage.Raw;
            dataset.UploadStatus = UploadStatus.Completed;
            dataset.BlobContainer = destination;
            _datasets.Update(dataset);
            await _datasets.SaveChangesAsync(ct);
        }
        catch
        {
            // Compensate storage promotion if the metadata cannot be committed.
            await _blob.MoveAsync(destination, dataset.BlobPath, source, dataset.BlobPath, CancellationToken.None);
            throw;
        }
        return MapToResponse(dataset);
    }

    private static void ValidateConfirmationMapping(
        IReadOnlyList<string> headers, MappingConfiguration mapping, string fieldType)
    {
        var fields = mapping.Fields.Where(f => f.MappingField?.IsActive == true).ToList();
        string[][] required = fieldType == "inventory"
            ? [["inv_product", "product", "product_id"], ["stock"]]
            : [["date"], ["product", "product_id"], ["quantity"]];
        foreach (var aliases in required)
        {
            var matches = fields.Where(f => aliases.Contains(f.MappingField!.FieldKey, StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(f.DatasetColumnName)).ToList();
            if (matches.Count == 0 || matches.Any(f => !headers.Contains(f.DatasetColumnName.Trim(), StringComparer.OrdinalIgnoreCase))
                || matches.Select(f => f.DatasetColumnName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                throw new ValidationException("mappingConfigurationId",
                    $"Falta una columna válida y no ambigua para el campo requerido '{aliases[0]}'.");
        }
        foreach (var field in fields.Where(f => f.MappingField!.FieldType == fieldType
            || required.Any(aliases => aliases.Contains(f.MappingField!.FieldKey, StringComparer.OrdinalIgnoreCase))))
            if (!string.IsNullOrWhiteSpace(field.DatasetColumnName)
                && !headers.Contains(field.DatasetColumnName.Trim(), StringComparer.OrdinalIgnoreCase))
                throw new ValidationException("mappingConfigurationId", $"La columna '{field.DatasetColumnName}' no existe en el archivo.");
    }

    public async Task DeleteAsync(Guid datasetId, Guid userId, CancellationToken ct = default)
    {
        var dataset = await _datasets.GetByIdAsync(datasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), datasetId);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        var details = await _datasets.GetWithDetailsAsync(datasetId, ct);
        if (dataset.DatasetStage != DatasetStage.Temporary
            || dataset.UploadStatus != UploadStatus.Pending
            || details?.PredictionExecutions.Count > 0)
            throw new ConflictException("Solo se pueden eliminar datasets temporales pendientes y sin uso.");

        if (dataset.BlobPath is not null && dataset.BlobContainer is not null)
        {
            try { await _blob.DeleteAsync(dataset.BlobContainer, dataset.BlobPath, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { throw new ExternalServiceException("Storage", "No se pudo eliminar el archivo temporal."); }
        }

        _datasets.Remove(dataset);
        await _datasets.SaveChangesAsync(ct);
    }

    // ── Validation helpers ────────────────────────────────────────────────────

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
        // Excel readers require seeking, which downloaded blob streams may not support.
        using var seekableCopy = new MemoryStream();
        if (!stream.CanSeek)
            stream.CopyTo(seekableCopy);
        seekableCopy.Position = 0;
        using var excelReader = ExcelReaderFactory.CreateReader(
            stream.CanSeek ? stream : seekableCopy, config);

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

        // Excel readers require seeking, which downloaded blob streams may not support.
        using var seekableCopy = new MemoryStream();
        if (!stream.CanSeek)
            stream.CopyTo(seekableCopy);
        seekableCopy.Position = 0;
        using var excelReader = ExcelReaderFactory.CreateReader(
            stream.CanSeek ? stream : seekableCopy, config);

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
            .Where(f => !string.IsNullOrWhiteSpace(f.DatasetColumnName)
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
