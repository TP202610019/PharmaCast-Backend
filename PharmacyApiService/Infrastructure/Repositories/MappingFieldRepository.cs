using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class MappingFieldRepository : GenericRepository<MappingField>, IMappingFieldRepository
{
    public MappingFieldRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<MappingField>> GetAllOrderedAsync(CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.FieldType)
            .ThenByDescending(f => f.IsRequired)
            .ThenBy(f => f.FieldKey)
            .ToListAsync(ct);

    public async Task<MappingField?> GetByKeyAsync(string fieldKey, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FieldKey == fieldKey, ct);
}
