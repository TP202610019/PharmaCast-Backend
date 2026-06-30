using Microsoft.AspNetCore.Http;
using PharmacyApiService.Presentation.DTOs.Dataset;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IDatasetService
{
    Task<DatasetResponse> UploadAsync(IFormFile file, Guid userId, Guid? mappingConfigId, CancellationToken ct = default);
    Task<DatasetResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DatasetResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<DatasetResponse> ValidateAsync(Guid datasetId, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid datasetId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ExtractHeadersAsync(Guid datasetId, Guid userId, CancellationToken ct = default);
    Task<Dictionary<string, double>> ReadInventoryStockAsync(Guid datasetId, Guid userId, string productColumn, string stockColumn, CancellationToken ct = default);
    IReadOnlyList<string> ParseFileHeaders(IFormFile file);
}
