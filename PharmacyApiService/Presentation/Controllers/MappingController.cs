using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Mapping;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/mappings")]
[Authorize]
public class MappingController : ControllerBase
{
    private readonly IMappingConfigService _service;

    public MappingController(IMappingConfigService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MappingConfigResponse>>> Create([FromBody] CreateMappingRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.CreateAsync(request, userId, ct);
        return Created(string.Empty, ApiResponse<MappingConfigResponse>.Ok(result, "Mapping configuration created."));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MappingConfigResponse>>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByUserAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<MappingConfigResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MappingConfigResponse>>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByIdAsync(id, userId, ct);
        return Ok(ApiResponse<MappingConfigResponse>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MappingConfigResponse>>> Update(Guid id, [FromBody] CreateMappingRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.UpdateAsync(id, request, userId, ct);
        return Ok(ApiResponse<MappingConfigResponse>.Ok(result, "Mapping configuration updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _service.DeleteAsync(id, userId, ct);
        return Ok(ApiResponse.Ok("Mapping configuration deleted."));
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult<ApiResponse>> SetDefault(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _service.SetDefaultAsync(id, userId, ct);
        return Ok(ApiResponse.Ok("Default mapping configuration updated."));
    }
}
