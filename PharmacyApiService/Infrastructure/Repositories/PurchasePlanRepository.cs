using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class PurchasePlanRepository : GenericRepository<PurchasePlan>, IPurchasePlanRepository
{
    public PurchasePlanRepository(ApplicationDbContext db) : base(db) { }

    public async Task<PurchasePlan?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(p => p.Items)
                     .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PurchasePlan?> GetByExecutionAsync(Guid predictionExecutionId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(p => p.Items)
                     .FirstOrDefaultAsync(p => p.PredictionExecutionId == predictionExecutionId, ct);
}
