using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Mapping;

namespace PharmacyApiService.Application.Services;

public class MappingFieldService : IMappingFieldService
{
    private readonly IMappingFieldRepository _repo;

    public MappingFieldService(IMappingFieldRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<MappingFieldResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var fields = await _repo.GetAllOrderedAsync(ct);
        return fields.Select(f => new MappingFieldResponse
        {
            Id          = f.Id,
            FieldKey    = f.FieldKey,
            DisplayName = f.DisplayName,
            FieldType   = f.FieldType,
            IsRequired  = f.IsRequired,
            IsActive    = f.IsActive,
        }).ToList();
    }
}
