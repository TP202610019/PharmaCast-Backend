using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class DatasetRepository : GenericRepository<Dataset>, IDatasetRepository
{
    public DatasetRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Dataset>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(d => d.UploadedBy == userId)
                     .OrderByDescending(d => d.CreatedAt)
                     .ToListAsync(ct);

    public async Task<Dataset?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(d => d.MappingConfiguration)
                     .Include(d => d.PredictionExecutions)
                     .FirstOrDefaultAsync(d => d.Id == id, ct);
}
