using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IPredictionRepository : IGenericRepository<PredictionExecution>
{
    Task<PredictionExecution?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PredictionExecution>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PredictionExecution>> GetByDatasetAsync(Guid datasetId, CancellationToken ct = default);
    Task SaveResultsAsync(PredictionExecution execution, IEnumerable<ForecastResult> forecasts,
        IEnumerable<PredictionMetric> metrics,
        CancellationToken ct = default);
}
