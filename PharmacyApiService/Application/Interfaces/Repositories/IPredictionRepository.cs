using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IPredictionRepository : IGenericRepository<PredictionExecution>
{
    Task<PredictionExecution?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PredictionExecution>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PredictionExecution>> GetByDatasetAsync(Guid datasetId, CancellationToken ct = default);
    Task AddForecastResultsAsync(IEnumerable<ForecastResult> results, CancellationToken ct = default);
    Task AddMetricsAsync(IEnumerable<PredictionMetric> metrics, CancellationToken ct = default);
    Task AddHistoricalPointsAsync(IEnumerable<HistoricalDailyPoint> points, CancellationToken ct = default);
}
