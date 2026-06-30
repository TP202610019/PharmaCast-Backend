using PharmacyApiService.Presentation.DTOs.Mapping;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IMappingConfigService
{
    Task<MappingConfigResponse> CreateAsync(CreateMappingRequest request, Guid userId, CancellationToken ct = default);
    Task<MappingConfigResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<MappingConfigResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<MappingConfigResponse> UpdateAsync(Guid id, CreateMappingRequest request, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default);
}
