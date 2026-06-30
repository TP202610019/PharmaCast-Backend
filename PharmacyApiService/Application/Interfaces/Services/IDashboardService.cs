using PharmacyApiService.Presentation.DTOs.Dashboard;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IDashboardService
{
    Task<DashboardMetricsResponse> GetMetricsAsync(Guid userId, CancellationToken ct = default);
    Task<DashboardSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken ct = default);
    Task<PagedResult<DashboardProductRow>> GetProductsAsync(Guid userId, Guid predictionId, int page, int pageSize, string? search, CancellationToken ct = default);
    Task<DashboardChartResponse> GetChartAsync(Guid userId, Guid predictionId, string productName, CancellationToken ct = default);
    Task<DashboardSummaryResponse> GetPredictionSummaryAsync(Guid predictionId, Guid userId, CancellationToken ct = default);
}
