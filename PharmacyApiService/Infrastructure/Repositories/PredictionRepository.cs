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

    public async Task AddForecastResultsAsync(IEnumerable<ForecastResult> results, CancellationToken ct = default)
    {
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var chunk in results.Chunk(500))
            {
                _db.ForecastResults.AddRange(chunk);
                await _db.SaveChangesAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    public async Task AddMetricsAsync(IEnumerable<PredictionMetric> metrics, CancellationToken ct = default)
    {
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var chunk in metrics.Chunk(500))
            {
                _db.PredictionMetrics.AddRange(chunk);
                await _db.SaveChangesAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    public async Task AddHistoricalPointsAsync(IEnumerable<HistoricalDailyPoint> points, CancellationToken ct = default)
    {
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var chunk in points.Chunk(500))
            {
                _db.HistoricalDailyPoints.AddRange(chunk);
                await _db.SaveChangesAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
