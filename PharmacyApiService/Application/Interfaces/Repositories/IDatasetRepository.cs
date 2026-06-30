using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IDatasetRepository : IGenericRepository<Dataset>
{
    Task<IReadOnlyList<Dataset>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Dataset?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
