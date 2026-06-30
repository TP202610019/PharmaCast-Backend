using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IPurchasePlanRepository : IGenericRepository<PurchasePlan>
{
    Task<PurchasePlan?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<PurchasePlan?> GetByExecutionAsync(Guid predictionExecutionId, CancellationToken ct = default);
}
