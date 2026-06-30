using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Dataset;
using PharmacyApiService.Shared.Constants;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/datasets")]
[Authorize]
public class DatasetController : ControllerBase
{
    private readonly IDatasetService _service;

    public DatasetController(IDatasetService service)
    {
        _service = service;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(AppConstants.MaxFileSizeBytes)]
    public async Task<ActionResult<ApiResponse<DatasetResponse>>> Upload(
        IFormFile file,
        [FromQuery] Guid? mappingConfigId,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.UploadAsync(file, userId, mappingConfigId, ct);
        return Created(string.Empty, ApiResponse<DatasetResponse>.Ok(result, "Dataset uploaded successfully."));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DatasetResponse>>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByUserAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<DatasetResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DatasetResponse>>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByIdAsync(id, userId, ct);
        return Ok(ApiResponse<DatasetResponse>.Ok(result));
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<ApiResponse<DatasetResponse>>> Validate(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.ValidateAsync(id, userId, ct);
        return Ok(ApiResponse<DatasetResponse>.Ok(result, "Dataset validated."));
    }

    [HttpGet("{id:guid}/headers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetHeaders(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var headers = await _service.ExtractHeadersAsync(id, userId, ct);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(headers));
    }

    [HttpPost("parse-headers")]
    [RequestSizeLimit(AppConstants.MaxFileSizeBytes)]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> ParseHeaders(IFormFile file)
    {
        var headers = _service.ParseFileHeaders(file);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(headers));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _service.DeleteAsync(id, userId, ct);
        return Ok(ApiResponse.Ok("Dataset deleted."));
    }
}
