using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Prediction;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/predictions")]
[Authorize]
public class PredictionController : ControllerBase
{
    private readonly IPredictionService _service;

    public PredictionController(IPredictionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PredictionResponse>>> Execute([FromBody] CreatePredictionRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.ExecuteAsync(request, userId, ct);
        return Created(string.Empty, ApiResponse<PredictionResponse>.Ok(result, "Prediction execution started."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PredictionResponse>>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByIdAsync(id, userId, ct);
        return Ok(ApiResponse<PredictionResponse>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PredictionSummaryResponse>>>> GetHistory(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetHistoryAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<PredictionSummaryResponse>>.Ok(result));
    }
}
