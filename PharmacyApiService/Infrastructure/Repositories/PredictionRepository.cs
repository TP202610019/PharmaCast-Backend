using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class PredictionRepository : GenericRepository<PredictionExecution>, IPredictionRepository
{
    public PredictionRepository(ApplicationDbContext db) : base(db) { }

    public async Task<PredictionExecution?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Dataset)
            .Include(p => p.ForecastResults)
            .Include(p => p.PredictionMetrics)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PredictionExecution>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(p => p.Dataset)
            .Where(p => p.ExecutedBy == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PredictionExecution>> GetByDatasetAsync(Guid datasetId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(p => p.DatasetId == datasetId)
                     .OrderByDescending(p => p.CreatedAt)
                     .ToListAsync(ct);

    public async Task SaveResultsAsync(PredictionExecution execution, IEnumerable<ForecastResult> forecasts,
        IEnumerable<PredictionMetric> metrics,
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // The backend owns every result row. All batches and Completed commit together.
            await SalesBulkWriter.ForecastAsync(_db, forecasts, ct);
            _db.PredictionMetrics.AddRange(metrics);
            _db.PredictionExecutions.Update(execution);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Do not retry rolled-back Added result entities when persisting Failed.
            _db.ChangeTracker.Clear();
            throw;
        }
    }
}
