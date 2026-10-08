using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmacyApiService.Application.Interfaces;
using PharmacyApiService.Application.Models.ML;
using PharmacyApiService.Domain.Exceptions;

namespace PharmacyApiService.Infrastructure.External.ML;

public class MLServiceClient : IMLServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MLServiceClient> _logger;
    private readonly MLSettings _settings;

    // FastAPI / Pydantic returns camelCase JSON; use camelCase for both directions.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MLServiceClient(HttpClient http, IOptions<MLSettings> options, ILogger<MLServiceClient> logger)
    {
        _http = http;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task<MLPredictionResponse> PredictAsync(MLPredictionRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Calling ML Service for execution {Id}, dataset {DatasetId}, horizon {Period}d",
            request.PredictionExecutionId, request.DatasetId, request.ForecastPeriod);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/api/predictions/execute", request, JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML Service is unreachable for execution {Id}.", request.PredictionExecutionId);
            throw new ExternalServiceException("ML Service", "The ML Service could not be reached. Make sure it is running.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "ML Service returned {Status} for execution {Id}. Body: {Body}",
                (int)response.StatusCode, request.PredictionExecutionId, body);

            // FastAPI's HTTPException serializes as {"detail": "..."} — surface that
            // real message instead of a generic "failed with status X", so it ends up
            // in prediction_executions.error_message instead of getting lost in logs.
            string? detail = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                    detail = detailProp.GetString();
            }
            catch (JsonException)
            {
                // Body wasn't JSON (e.g. a proxy/error page) — fall back to the generic message below.
            }

            throw new ExternalServiceException("ML Service",
                detail ?? $"The prediction request failed with status {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<MLPredictionResponse>(JsonOptions, ct);

        if (result is null)
        {
            _logger.LogError("ML Service returned an empty response for execution {Id}.", request.PredictionExecutionId);
            throw new ExternalServiceException("ML Service", "The prediction service returned an empty response.");
        }

        if (!result.Success)
        {
            _logger.LogError(
                "ML Service reported failure for execution {Id}: {Error}",
                request.PredictionExecutionId, result.ErrorMessage);
            throw new ExternalServiceException("ML Service",
                result.ErrorMessage ?? "The prediction execution failed in the ML Service.");
        }

        _logger.LogInformation(
            "ML Service completed execution {Id}: {Products} products, {Forecasts} forecast entries",
            result.PredictionExecutionId, result.ProductMetrics.Count, result.Forecast.Count);

        return result;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML Service health check failed.");
            return false;
        }
    }

}
