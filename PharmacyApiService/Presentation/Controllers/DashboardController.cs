using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Dashboard;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<ApiResponse<DashboardMetricsResponse>>> GetMetrics(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetMetricsAsync(userId, ct);
        return Ok(ApiResponse<DashboardMetricsResponse>.Ok(result));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryResponse>>> GetSummary(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetSummaryAsync(userId, ct);
        return Ok(ApiResponse<DashboardSummaryResponse>.Ok(result));
    }

    [HttpGet("products")]
    public async Task<ActionResult<ApiResponse<PagedResult<DashboardProductRow>>>> GetProducts(
        [FromQuery] Guid predictionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _service.GetProductsAsync(userId, predictionId, page, pageSize, search, ct);
        return Ok(ApiResponse<PagedResult<DashboardProductRow>>.Ok(result));
    }

    [HttpGet("prediction-summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryResponse>>> GetPredictionSummary(
        [FromQuery] Guid predictionId,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetPredictionSummaryAsync(predictionId, userId, ct);
        return Ok(ApiResponse<DashboardSummaryResponse>.Ok(result));
    }

    [HttpGet("chart")]
    public async Task<ActionResult<ApiResponse<DashboardChartResponse>>> GetChart(
        [FromQuery] Guid predictionId,
        [FromQuery] string product,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _service.GetChartAsync(userId, predictionId, product, ct);
        return Ok(ApiResponse<DashboardChartResponse>.Ok(result));
    }
}
