using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IMappingConfigRepository : IGenericRepository<MappingConfiguration>
{
    Task<IReadOnlyList<MappingConfiguration>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<MappingConfiguration?> GetDefaultForUserAsync(Guid userId, CancellationToken ct = default);
    Task<MappingConfiguration?> GetByNameAsync(Guid userId, string name, CancellationToken ct = default);
    Task<MappingConfiguration?> GetByIdWithFieldsAsync(Guid id, CancellationToken ct = default);
    Task<MappingConfiguration?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default);
}
