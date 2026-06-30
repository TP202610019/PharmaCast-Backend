using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Mapping;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/mapping-fields")]
[Authorize]
public class MappingFieldController : ControllerBase
{
    private readonly IMappingFieldService _service;

    public MappingFieldController(IMappingFieldService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MappingFieldResponse>>>> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MappingFieldResponse>>.Ok(result));
    }
}
