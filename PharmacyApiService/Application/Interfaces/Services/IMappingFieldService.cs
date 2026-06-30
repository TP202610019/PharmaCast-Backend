using PharmacyApiService.Presentation.DTOs.Mapping;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IMappingFieldService
{
    Task<IReadOnlyList<MappingFieldResponse>> GetAllAsync(CancellationToken ct = default);
}
