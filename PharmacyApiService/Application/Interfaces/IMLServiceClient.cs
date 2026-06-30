using PharmacyApiService.Application.Models.ML;

namespace PharmacyApiService.Application.Interfaces;

public interface IMLServiceClient
{
    Task<MLPredictionResponse> PredictAsync(MLPredictionRequest request, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
