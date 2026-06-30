using Microsoft.AspNetCore.Http;
using PharmacyApiService.Presentation.DTOs.PurchasePlan;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IPurchasePlanService
{
    Task<PurchasePlanResponse> GenerateAsync(GeneratePurchasePlanRequest request, Guid userId, CancellationToken ct = default);
    Task<PurchasePlanResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<PurchasePlanResponse> GetByExecutionAsync(Guid predictionExecutionId, Guid userId, CancellationToken ct = default);
    Task<PlanEvaluationResponse> EvaluateAsync(
        Guid planId,
        IFormFile actualSalesFile,
        string productCol,
        string quantityCol,
        string? priceCol,
        IFormFile? pharmacyPlanFile,
        string? pharmacyProductCol,
        string? pharmacyQuantityCol,
        Guid userId,
        CancellationToken ct = default);

    Task<PlanEvaluationResponse?> GetLatestEvaluationAsync(Guid planId, Guid userId, CancellationToken ct = default);
}
