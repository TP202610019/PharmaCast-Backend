using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class MappingConfigRepository : GenericRepository<MappingConfiguration>, IMappingConfigRepository
{
    public MappingConfigRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<MappingConfiguration>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(m => m.Fields).ThenInclude(f => f.MappingField)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task<MappingConfiguration?> GetDefaultForUserAsync(Guid userId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(m => m.Fields).ThenInclude(f => f.MappingField)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsDefault, ct);

    public async Task<MappingConfiguration?> GetByNameAsync(Guid userId, string name, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(m => m.Fields).ThenInclude(f => f.MappingField)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ConfigurationName == name, ct);

    public async Task<MappingConfiguration?> GetByIdWithFieldsAsync(Guid id, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(m => m.Fields).ThenInclude(f => f.MappingField)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MappingConfiguration?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default)
        => await _set
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
}
