using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Infrastructure.Persistence;
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

    private readonly ApplicationDbContext _db;

    public PredictionController(IPredictionService service, ApplicationDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpGet("periods")]
    public async Task<IActionResult> GetPeriods(CancellationToken ct)
        => Ok(await _db.ForecastHorizons.AsNoTracking().OrderBy(p => p.Days)
            .Select(p => new { p.Days, p.DisplayName, p.IsActive }).ToListAsync(ct));

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
