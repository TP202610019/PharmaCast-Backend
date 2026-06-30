using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IMappingFieldRepository : IGenericRepository<MappingField>
{
    Task<IReadOnlyList<MappingField>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<MappingField?> GetByKeyAsync(string fieldKey, CancellationToken ct = default);
}
