using PharmacyApiService.Application.Interfaces;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Application.Models.ML;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Domain.Enums;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Presentation.DTOs.Prediction;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly IPredictionRepository _predictions;
    private readonly IDatasetRepository _datasets;
    private readonly IMappingConfigRepository _mappings;
    private readonly IMLServiceClient _mlClient;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(
        IPredictionRepository predictions,
        IDatasetRepository datasets,
        IMappingConfigRepository mappings,
        IMLServiceClient mlClient,
        IBlobStorageService blob,
        ILogger<PredictionService> logger)
    {
        _predictions = predictions;
        _datasets = datasets;
        _mappings = mappings;
        _mlClient = mlClient;
        _blob = blob;
        _logger = logger;
    }

    public async Task<PredictionResponse> ExecuteAsync(
        CreatePredictionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[PREDICT-START] ExecuteAsync — datasetId={DatasetId}, userId={UserId}, forecastPeriod={Period}, mappingId={MappingId}",
            request.DatasetId, userId, request.ForecastPeriod, request.MappingConfigurationId);

        // ── 1. Resolve dataset ────────────────────────────────────────────────
        _logger.LogInformation("[PREDICT-1] Querying dataset {DatasetId} from DB...", request.DatasetId);
        var dataset = await _datasets.GetByIdAsync(request.DatasetId, ct)
            ?? throw new NotFoundException(nameof(Dataset), request.DatasetId);

        _logger.LogInformation(
            "[PREDICT-1] Dataset found — file={File}, stage={Stage}, status={Status}, container={Container}, blobPath={BlobPath}",
            dataset.OriginalFileName, dataset.DatasetStage, dataset.UploadStatus,
            dataset.BlobContainer, dataset.BlobPath);

        if (dataset.UploadedBy != userId)
            throw new ForbiddenException();

        if (dataset.UploadStatus != UploadStatus.Completed || dataset.DatasetStage != DatasetStage.Raw)
            throw new ValidationException("dataset",
                $"El dataset debe estar completamente validado (stage=Raw, status=Completed) antes de ejecutar una predicción. " +
                $"Estado actual: stage={dataset.DatasetStage}, status={dataset.UploadStatus}.");

        // ── 2. Resolve mapping configuration ─────────────────────────────────
        _logger.LogInformation(
            "[PREDICT-2] Resolving mapping — requestMappingId={ReqMapping}, datasetMappingId={DsMapping}",
            request.MappingConfigurationId, dataset.MappingConfigurationId);

        MappingConfiguration? mapping = null;
        if (request.MappingConfigurationId.HasValue)
            mapping = await _mappings.GetByIdWithFieldsAsync(request.MappingConfigurationId.Value, ct);
        if (mapping is null && dataset.MappingConfigurationId.HasValue)
            mapping = await _mappings.GetByIdWithFieldsAsync(dataset.MappingConfigurationId.Value, ct);
        if (mapping is null)
        {
            var def = await _mappings.GetDefaultForUserAsync(userId, ct);
            if (def is not null)
                mapping = await _mappings.GetByIdWithFieldsAsync(def.Id, ct) ?? def;
        }

        if (mapping is null)
            throw new ValidationException("mapping",
                "No se encontró una configuración de mapeo de columnas. " +
                "Por favor, define el mapeo de columnas antes de ejecutar la predicción.");

        _logger.LogInformation(
            "[PREDICT-2] Mapping resolved — id={MappingId}, fields={Fields}",
            mapping.Id,
            string.Join(", ", mapping.Fields.Select(f => $"{f.MappingField?.FieldKey}='{f.DatasetColumnName}'")));

        // ── 3. Create execution record in DB ──────────────────────────────────
        // Collect response data in-memory to avoid a GetWithDetailsAsync round-trip
        // at the end — that query generates a Cartesian JOIN over 20k+ forecast rows.
        List<ForecastResultResponse> forecastForResponse = [];
        List<PredictionMetricResponse> metricsForResponse = [];

        var execution = new PredictionExecution
        {
            DatasetId = dataset.Id,
            ExecutedBy = userId,
            ForecastPeriod = request.ForecastPeriod,
            ExecutionStatus = ExecutionStatus.Processing,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("[PREDICT-3] Persisting execution record to DB (AddAsync + SaveChangesAsync)...");
        await _predictions.AddAsync(execution, ct);
        await _predictions.SaveChangesAsync(ct);
        _logger.LogInformation("[PREDICT-3] Execution record created — executionId={ExecutionId}", execution.Id);

        // ── 4. Resolve dataset path and call ML Service ───────────────────────
        try
        {
            var container = dataset.BlobContainer ?? AppConstants.Blob.RawDatasetContainer;
            var blobPath  = dataset.BlobPath ?? string.Empty;
            var datasetPath = _blob.GenerateSasUrl(container, blobPath, TimeSpan.FromMinutes(30));

            _logger.LogInformation(
                "[PREDICT-4] Dataset path resolved — container={Container}, blobPath={BlobPath}",
                container, blobPath);

            var fieldDict = mapping.Fields
                .Where(f => f.MappingField is not null)
                .ToDictionary(f => f.MappingField!.FieldKey, f => f.DatasetColumnName);

            var mlRequest = new MLPredictionRequest
            {
                PredictionExecutionId = execution.Id.ToString(),
                DatasetId             = dataset.Id.ToString(),
                UserId                = userId.ToString(),
                DatasetPath           = datasetPath,
                ForecastPeriod        = request.ForecastPeriod,
                FieldMappings         = new MLFieldMappings { Fields = fieldDict }
            };

            _logger.LogInformation(
                "[PREDICT-5] Calling ML Service — executionId={Id}, datasetId={DatasetId}, path={Path}, " +
                "period={Period}, fields={Fields}",
                mlRequest.PredictionExecutionId, mlRequest.DatasetId, mlRequest.DatasetPath,
                mlRequest.ForecastPeriod,
                string.Join(", ", fieldDict.Select(kv => $"{kv.Key}='{kv.Value}'")));

            // Use CancellationToken.None so the ML processing completes even if the
            // HTTP client disconnects mid-flight (ML takes 1-3 minutes for large datasets).
            var mlResponse = await _mlClient.PredictAsync(mlRequest, CancellationToken.None);

            _logger.LogInformation(
                "[PREDICT-5] ML Service responded — status={Status}, forecastEntries={ForecastCount}, productMetrics={ProductCount}",
                mlResponse.Status, mlResponse.Forecast.Count, mlResponse.ProductMetrics.Count);

            // ── 5a. Persist forecast results ──────────────────────────────────
            var forecastResults = mlResponse.Forecast
                .Where(f => DateTime.TryParse(f.Date, out _))
                .Select(f =>
                {
                    var rawDate = DateTime.Parse(f.Date, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    // Npgsql 9.x requires Kind=Utc for 'timestamp with time zone' — treat naive dates as UTC
                    var utcDate = rawDate.Kind == DateTimeKind.Utc
                        ? rawDate
                        : DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);

                    _logger.LogDebug(
                        "[PERSIST-FORECAST] productId={Product} rawDate='{Raw}' parsedKind={Kind} utcDate='{Utc}'",
                        f.ProductId, f.Date, rawDate.Kind, utcDate);

                    return new ForecastResult
                    {
                        PredictionExecutionId = execution.Id,
                        ProductName           = f.ProductId,
                        ForecastDate          = utcDate,
                        PredictedQuantity     = f.PredictedQuantity,
                        LowerBound            = null,
                        UpperBound            = null
                    };
                });

            // ── 5b. Persist metrics ───────────────────────────────────────────
            var allMetrics = new[] { BuildOverallMetric(execution.Id, mlResponse.Metrics) }
                .Concat(mlResponse.ProductMetrics.Select(pm => BuildProductMetric(execution.Id, pm)));

            // Build response DTOs from in-memory data before saving to DB
            forecastForResponse = forecastResults
                .Select(f => new ForecastResultResponse
                {
                    ProductName       = f.ProductName,
                    ForecastDate      = f.ForecastDate,
                    PredictedQuantity = f.PredictedQuantity,
                    LowerBound        = f.LowerBound,
                    UpperBound        = f.UpperBound,
                }).ToList();

            var allMetricsList = allMetrics.ToList();
            metricsForResponse = allMetricsList
                .Select(m => new PredictionMetricResponse
                {
                    ProductName   = m.ProductName,
                    Wape          = m.Wape,
                    Rmse          = m.Rmse,
                    Mae           = m.Mae,
                    Mape          = m.Mape,
                    Rows          = m.Rows,
                    HistoricalAvg = m.HistoricalAvg,
                }).ToList();

            _logger.LogInformation("[PREDICT-6] Persisting forecast results and metrics to DB...");
            await _predictions.AddForecastResultsAsync(forecastResults, CancellationToken.None);
            await _predictions.AddMetricsAsync(allMetricsList, CancellationToken.None);

            execution.ExecutionStatus = ExecutionStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("[PREDICT-6] Forecast and metrics saved ({F} rows, {M} rows).",
                forecastForResponse.Count, metricsForResponse.Count);

            // ── 5c. Persist historical daily points ───────────────────────────
            if (mlResponse.HistoricalPoints.Count > 0)
            {
                var historicalPoints = mlResponse.HistoricalPoints
                    .Where(h => DateTime.TryParse(h.Date, out _))
                    .Select(h =>
                    {
                        var rawDate = DateTime.Parse(h.Date, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        var utcDate = rawDate.Kind == DateTimeKind.Utc
                            ? rawDate
                            : DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);
                        return new HistoricalDailyPoint
                        {
                            PredictionExecutionId = execution.Id,
                            ProductName = h.ProductId,
                            Date = utcDate,
                            Quantity = h.Quantity
                        };
                    });
                await _predictions.AddHistoricalPointsAsync(historicalPoints, CancellationToken.None);
                _logger.LogInformation("[PREDICT-6] Historical daily points saved ({Count} rows).", mlResponse.HistoricalPoints.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or Axios timeout fired — ct is already cancelled.
            // Mark as Failed so the record is never left orphaned in Processing state.
            _logger.LogWarning(
                "[PREDICT-ERROR] Execution cancelled (client timeout or disconnect) — executionId={Id}", execution.Id);

            execution.ExecutionStatus = ExecutionStatus.Failed;
            execution.ErrorMessage = "La predicción fue cancelada por tiempo de espera.";
            execution.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PREDICT-ERROR] Execution failed — exceptionType={ExType}, message={Message}",
                ex.GetType().Name, ex.Message);

            execution.ExecutionStatus = ExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;
        }

        // ── 7. Save final execution state ─────────────────────────────────────
        // Use CancellationToken.None: the original ct may already be cancelled (client timeout),
        // but the execution state must always be persisted to avoid orphaned Processing records.
        _logger.LogInformation(
            "[PREDICT-7] Saving final execution state — status={Status}...", execution.ExecutionStatus);

        execution.UpdatedAt = DateTime.UtcNow;
        _predictions.Update(execution);
        await _predictions.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation(
            "[PREDICT-8] Response ready — executionId={Id}, status={Status}, forecastRows={FRows}, metrics={MRows}",
            execution.Id, execution.ExecutionStatus, forecastForResponse.Count, metricsForResponse.Count);

        return new PredictionResponse
        {
            Id                 = execution.Id,
            DatasetId          = execution.DatasetId,
            ForecastPeriod     = execution.ForecastPeriod,
            ExecutionStatus    = execution.ExecutionStatus,
            StartedAt          = execution.StartedAt,
            CompletedAt        = execution.CompletedAt,
            ErrorMessage       = execution.ErrorMessage,
            CreatedAt          = execution.CreatedAt,
            ForecastResults    = forecastForResponse,
            Metrics            = metricsForResponse,
        };
    }

    public async Task<PredictionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var execution = await _predictions.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(PredictionExecution), id);

        if (execution.ExecutedBy != userId)
            throw new ForbiddenException();

        return MapToResponse(execution);
    }

    public async Task<IReadOnlyList<PredictionSummaryResponse>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var executions = await _predictions.GetByUserAsync(userId, ct);
        return executions.Select(e => new PredictionSummaryResponse
        {
            Id              = e.Id,
            DatasetId       = e.DatasetId,
            DatasetName     = e.Dataset?.OriginalFileName ?? string.Empty,
            ForecastPeriod  = e.ForecastPeriod,
            ExecutionStatus = e.ExecutionStatus,
            CompletedAt     = e.CompletedAt,
            CreatedAt       = e.CreatedAt
        }).ToList();
    }

    // ── Metric helpers ────────────────────────────────────────────────────────

    private static PredictionMetric BuildOverallMetric(Guid executionId, MLExecutionMetrics m) => new()
    {
        PredictionExecutionId = executionId,
        ProductName = null,
        Wape = m.Wape,
        Rmse = m.Rmse,
        Mae  = m.Mae,
        Mape = m.Mape,
    };

    private static PredictionMetric BuildProductMetric(Guid executionId, MLProductMetric pm) => new()
    {
        PredictionExecutionId = executionId,
        ProductName   = pm.ProductId,
        Wape          = pm.Wape,
        Rmse          = pm.Rmse,
        Mae           = pm.Mae,
        Mape          = pm.Mape,
        Rows          = pm.Rows,
        HistoricalAvg = pm.HistoricalAvg,
    };

    // ── Response mapping ──────────────────────────────────────────────────────

    private static PredictionResponse MapToResponse(PredictionExecution e) => new()
    {
        Id              = e.Id,
        DatasetId       = e.DatasetId,
        ForecastPeriod  = e.ForecastPeriod,
        ExecutionStatus = e.ExecutionStatus,
        StartedAt       = e.StartedAt,
        CompletedAt     = e.CompletedAt,
        ErrorMessage    = e.ErrorMessage,
        CreatedAt       = e.CreatedAt,
        ForecastResults = e.ForecastResults.Select(f => new ForecastResultResponse
        {
            ProductName       = f.ProductName,
            ForecastDate      = f.ForecastDate,
            PredictedQuantity = f.PredictedQuantity,
            LowerBound        = f.LowerBound,
            UpperBound        = f.UpperBound
        }).ToList(),
        Metrics = e.PredictionMetrics.Select(m => new PredictionMetricResponse
        {
            ProductName   = m.ProductName,
            Wape          = m.Wape,
            Rmse          = m.Rmse,
            Mae           = m.Mae,
            Mape          = m.Mape,
            Rows          = m.Rows,
            HistoricalAvg = m.HistoricalAvg,
        }).ToList()
    };
}
