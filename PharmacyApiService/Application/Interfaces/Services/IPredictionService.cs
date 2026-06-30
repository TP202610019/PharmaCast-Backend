using PharmacyApiService.Presentation.DTOs.Prediction;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IPredictionService
{
    Task<PredictionResponse> ExecuteAsync(CreatePredictionRequest request, Guid userId, CancellationToken ct = default);
    Task<PredictionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PredictionSummaryResponse>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
}
