using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.PurchasePlan;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/purchase-plans")]
[Authorize]
public class PurchasePlanController : ControllerBase
{
    private readonly IPurchasePlanService _service;

    public PurchasePlanController(IPurchasePlanService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchasePlanResponse>>> Generate([FromBody] GeneratePurchasePlanRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GenerateAsync(request, userId, ct);
        return Created(string.Empty, ApiResponse<PurchasePlanResponse>.Ok(result, "Purchase plan generated."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchasePlanResponse>>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByIdAsync(id, userId, ct);
        return Ok(ApiResponse<PurchasePlanResponse>.Ok(result));
    }

    [HttpGet("by-execution/{executionId:guid}")]
    public async Task<ActionResult<ApiResponse<PurchasePlanResponse>>> GetByExecution(Guid executionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetByExecutionAsync(executionId, userId, ct);
        return Ok(ApiResponse<PurchasePlanResponse>.Ok(result));
    }

    [HttpPost("{id:guid}/evaluate")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<PlanEvaluationResponse>>> Evaluate(
        Guid id,
        IFormFile actualSalesFile,
        [FromForm] string productCol,
        [FromForm] string quantityCol,
        [FromForm] string? priceCol,
        IFormFile? pharmacyPlanFile,
        [FromForm] string? pharmacyProductCol,
        [FromForm] string? pharmacyQuantityCol,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.EvaluateAsync(id, actualSalesFile, productCol, quantityCol,
            priceCol, pharmacyPlanFile, pharmacyProductCol, pharmacyQuantityCol, userId, ct);
        return Ok(ApiResponse<PlanEvaluationResponse>.Ok(result, "Evaluación completada."));
    }

    [HttpGet("{id:guid}/evaluation/latest")]
    public async Task<ActionResult<ApiResponse<PlanEvaluationResponse>>> GetLatestEvaluation(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _service.GetLatestEvaluationAsync(id, userId, ct);
        if (result is null)
            return NotFound(ApiResponse<PlanEvaluationResponse>.Fail("NOT_FOUND", "No hay evaluaciones guardadas para este plan."));
        return Ok(ApiResponse<PlanEvaluationResponse>.Ok(result));
    }
}
