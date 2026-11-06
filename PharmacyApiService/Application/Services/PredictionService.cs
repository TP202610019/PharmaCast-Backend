using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Infrastructure.Persistence;
using PharmacyApiService.Presentation.DTOs.Dataset;
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
    private readonly ApplicationDbContext _db;
    private readonly IDatasetService _datasetService;
    private readonly IDatasetRepository _datasets;
    private readonly IMappingConfigRepository _mappings;
    private readonly IMLServiceClient _mlClient;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(
        IPredictionRepository predictions,
        ApplicationDbContext db,
        IDatasetService datasetService,
        IDatasetRepository datasets,
        IMappingConfigRepository mappings,
        IMLServiceClient mlClient,
        IBlobStorageService blob,
        ILogger<PredictionService> logger)
    {
        _predictions = predictions;
        _db = db;
        _datasetService = datasetService;
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

        if (!await _db.ForecastHorizons.AsNoTracking()
            .AnyAsync(p => p.Days == request.ForecastPeriod && p.IsActive, ct))
            throw new ValidationException("forecastPeriod", "Selecciona un período de pronóstico activo del catálogo.");

        // ── 2. Resolve mapping configuration ─────────────────────────────────
        _logger.LogInformation(
            "[PREDICT-2] Resolving mapping — requestMappingId={ReqMapping}, datasetMappingId={DsMapping}",
            request.MappingConfigurationId, dataset.MappingConfigurationId);

        MappingConfiguration? mapping = null;
        if (request.MappingConfigurationId.HasValue)
            mapping = await _mappings.GetByIdWithFieldsAsync(request.MappingConfigurationId.Value, ct)
                ?? throw new NotFoundException(nameof(MappingConfiguration), request.MappingConfigurationId.Value);
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

        if (mapping.UserId != userId) throw new ForbiddenException();
        // Compatibility: prediction confirms using the same sales validation as the explicit endpoint.
        await _datasetService.ConfirmAsync(dataset.Id, userId,
            new ConfirmDatasetRequest { MappingConfigurationId = mapping.Id }, ct);
        dataset = await _datasets.GetByIdAsync(dataset.Id, ct)
            ?? throw new NotFoundException(nameof(Dataset), request.DatasetId);

        _logger.LogInformation(
            "[PREDICT-2] Mapping resolved — id={MappingId}, fields={Fields}",
            mapping.Id,
            string.Join(", ", mapping.Fields.Select(f => $"{f.MappingField?.FieldKey}='{f.DatasetColumnName}'")));

        // ── 3. Create execution record in DB ──────────────────────────────────
        // Collect response data in-memory to avoid a GetWithDetailsAsync round-trip
        // at the end — that query generates a Cartesian JOIN over 20k+ forecast rows.
        List<ForecastResultResponse> forecastForResponse = [];
        List<PredictionMetricResponse> metricsForResponse = [];
        bool isDemo = false;

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
        using var processingBudget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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
                "[PREDICT-5] Calling ML Service — executionId={Id}, datasetId={DatasetId}, " +
                "period={Period}, fields={Fields}",
                mlRequest.PredictionExecutionId, mlRequest.DatasetId,
                mlRequest.ForecastPeriod,
                string.Join(", ", fieldDict.Select(kv => $"{kv.Key}='{kv.Value}'")));

            // One shared five-minute budget covers ML and persistence.
            // Client disconnects do not cancel the execution independently.
            var mlResponse = await _mlClient.PredictAsync(mlRequest, processingBudget.Token);
            await SaveHistoryAsync(dataset.Id, mlResponse.HistoricalPoints, processingBudget.Token);
            isDemo = mlResponse.IsDemo;
            execution.IsRetrain = mlResponse.IsRetrain;
            execution.ArtifactPaths = mlResponse.ArtifactPaths;
            execution.Warnings = mlResponse.Warnings;
            execution.TrainingPeriodStart = mlResponse.TrainingPeriodStart;
            execution.TrainingPeriodEnd = mlResponse.TrainingPeriodEnd;
            execution.EvaluationPeriodStart = mlResponse.EvaluationPeriodStart;
            execution.EvaluationPeriodEnd = mlResponse.EvaluationPeriodEnd;
            execution.NewProductsCount = mlResponse.NewProductsCount;
            execution.NewProductsUnits = mlResponse.NewProductsUnits;

            _logger.LogInformation(
                "[PREDICT-5] ML Service responded — status={Status}, forecastEntries={ForecastCount}, productMetrics={ProductCount}",
                mlResponse.Status, mlResponse.Forecast.Count, mlResponse.ProductMetrics.Count);

            // ── 5a. Persist forecast results ──────────────────────────────────
            var forecastResults = mlResponse.Forecast
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
                        ForecastStep          = f.ForecastStep,
                        PredictedQuantity     = f.PredictedQuantity,
                        LowerBound            = null,
                        UpperBound            = null
                    };
                }).ToList();

            // ── 5b. Persist metrics ───────────────────────────────────────────
            var allMetrics = new[] { BuildOverallMetric(execution.Id, mlResponse.Metrics) }
                .Concat(mlResponse.ProductMetrics.Select(pm => BuildProductMetric(execution.Id, pm)))
                .Concat(mlResponse.BaselineMetrics is null ? Array.Empty<PredictionMetric>()
                    : new[] { BuildOverallMetric(execution.Id, mlResponse.BaselineMetrics, MetricMethod.NaiveWeekly) })
                .Concat(mlResponse.BaselineProductMetrics.Select(pm =>
                    BuildProductMetric(execution.Id, pm, MetricMethod.NaiveWeekly)));

            // Build response DTOs from in-memory data before saving to DB
            forecastForResponse = forecastResults
                .Select(f => new ForecastResultResponse
                {
                    ProductName       = f.ProductName,
                    ForecastDate      = f.ForecastDate,
                    ForecastStep      = f.ForecastStep,
                    PredictedQuantity = f.PredictedQuantity,
                    LowerBound        = f.LowerBound,
                    UpperBound        = f.UpperBound,
                }).ToList();

            var allMetricsList = allMetrics.ToList();
            metricsForResponse = allMetricsList
                .Select(m => new PredictionMetricResponse
                {
                    ProductName   = m.ProductName,
                    Method        = m.Method,
                    Scope         = m.Scope,
                    Mbe           = m.Mbe,
                    ActualTotal = m.ActualTotal,
                    PredictedTotal = m.PredictedTotal,
                    AbsoluteTotalError = m.AbsoluteTotalError,
                    TotalErrorPercentage = m.TotalErrorPercentage,
                    Wape          = m.Wape,
                    Rmse          = m.Rmse,
                    Mae           = m.Mae,
                    Mape          = m.Mape,
                    Rows          = m.Rows,
                    HistoricalAvg = m.HistoricalAvg,
                }).ToList();

            _logger.LogInformation("[PREDICT-6] Persisting forecast results and metrics to DB...");
            var forecastList = forecastResults.ToList();

            execution.ExecutionStatus = ExecutionStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("[PREDICT-6] Forecast and metrics saved ({F} rows, {M} rows).",
                forecastForResponse.Count, metricsForResponse.Count);

            await _predictions.SaveResultsAsync(execution, forecastList, allMetricsList,
                processingBudget.Token);
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

        if (execution.ExecutionStatus == ExecutionStatus.Failed)
        {
            forecastForResponse.Clear();
            metricsForResponse.Clear();
            execution.TrainingPeriodStart = execution.TrainingPeriodEnd = null;
            execution.EvaluationPeriodStart = execution.EvaluationPeriodEnd = null;
            execution.NewProductsCount = null;
            execution.NewProductsUnits = null;
            execution.UpdatedAt = DateTime.UtcNow;
            _predictions.Update(execution);
            await _predictions.SaveChangesAsync(CancellationToken.None);
        }

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
            IsDemo             = isDemo,
            IsRetrain = execution.IsRetrain,
            ArtifactPaths = execution.ArtifactPaths,
            Warnings = execution.Warnings,
            CreatedAt          = execution.CreatedAt,
            ForecastResults    = forecastForResponse,
            Metrics            = metricsForResponse.Where(m => m.Method == MetricMethod.XGBoost).ToList(),
            ProductMetrics = metricsForResponse.Where(m => m.Method == MetricMethod.XGBoost && m.Scope == MetricScope.Product).ToList(),
            BaselineMetrics = metricsForResponse.Where(m => m.Method == MetricMethod.NaiveWeekly).ToList(),
            BaselineProductMetrics = metricsForResponse.Where(m => m.Method == MetricMethod.NaiveWeekly && m.Scope == MetricScope.Product).ToList(),
            TrainingPeriodStart = execution.TrainingPeriodStart,
            TrainingPeriodEnd = execution.TrainingPeriodEnd,
            EvaluationPeriodStart = execution.EvaluationPeriodStart,
            EvaluationPeriodEnd = execution.EvaluationPeriodEnd,
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

    private async Task SaveHistoryAsync(Guid datasetId, List<MLHistoricalEntry> points, CancellationToken ct)
    {
        if (points.Count == 0) throw new InvalidOperationException("El dataset no contiene ventas válidas.");
        var rows = points.Select(p => new HistoricalDailyPoint
        {
            DatasetId = datasetId, ProductName = p.ProductId,
            Date = DateTime.SpecifyKind(DateTime.ParseExact(p.Date, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc),
            Quantity = p.Quantity
        }).ToList();
        if (rows.Any(r => string.IsNullOrWhiteSpace(r.ProductName) || !double.IsFinite(r.Quantity)) ||
            rows.Select(r => (r.ProductName, r.Date)).Distinct().Count() != rows.Count)
            throw new InvalidOperationException("El histórico diario recibido es inválido.");
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize preparation for the same dataset across backend instances.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({datasetId.ToString()}, 0))", ct);
            // Zero-demand days are reconstructed for charts. Compare only persisted sales
            // rows so older dense snapshots remain compatible with the sparse format.
            var existing = await _db.HistoricalDailyPoints.AsNoTracking()
                .Where(h => h.DatasetId == datasetId && h.Quantity != 0).ToListAsync(ct);
            if (existing.Count > 0)
            {
                var expected = rows.ToDictionary(r => (r.ProductName, r.Date), r => r.Quantity);
                if (existing.Count != rows.Count || existing.Any(r =>
                    !expected.TryGetValue((r.ProductName, r.Date), out var quantity) || quantity != r.Quantity))
                    throw new InvalidOperationException("El histórico del dataset cambió. Carga un nuevo dataset.");
            }
            else
            {
                await SalesBulkWriter.HistoryAsync(_db, rows, ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    // ── Metric helpers ────────────────────────────────────────────────────────

    private static PredictionMetric BuildOverallMetric(Guid executionId, MLExecutionMetrics m, MetricMethod method = MetricMethod.XGBoost) => new()
    {
        PredictionExecutionId = executionId,
        ProductName = null,
        Method = method,
        Scope = MetricScope.Overall,
        Rows = m.Rows,
        Mbe = m.Mbe,
        ActualTotal = m.ActualTotal,
        PredictedTotal = m.PredictedTotal,
        AbsoluteTotalError = m.AbsoluteTotalError,
        TotalErrorPercentage = m.TotalErrorPercentage,
        Wape = m.Wape,
        Rmse = m.Rmse,
        Mae  = m.Mae,
        Mape = m.Mape,
    };

    private static PredictionMetric BuildProductMetric(Guid executionId, MLProductMetric pm, MetricMethod method = MetricMethod.XGBoost) => new()
    {
        PredictionExecutionId = executionId,
        ProductName   = pm.ProductId,
        Method = method,
        Scope = MetricScope.Product,
        Mbe = pm.Mbe,
        ActualTotal = pm.ActualTotal,
        PredictedTotal = pm.PredictedTotal,
        AbsoluteTotalError = pm.AbsoluteTotalError,
        TotalErrorPercentage = pm.TotalErrorPercentage,
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
        TrainingPeriodStart = e.TrainingPeriodStart,
        TrainingPeriodEnd = e.TrainingPeriodEnd,
        EvaluationPeriodStart = e.EvaluationPeriodStart,
        EvaluationPeriodEnd = e.EvaluationPeriodEnd,
        ErrorMessage    = e.ErrorMessage,
        IsRetrain = e.IsRetrain,
        ArtifactPaths = e.ArtifactPaths,
        Warnings = e.Warnings,
        CreatedAt       = e.CreatedAt,
        ForecastResults = e.ForecastResults.Select(f => new ForecastResultResponse
        {
            ProductName       = f.ProductName,
            ForecastDate      = f.ForecastDate,
            ForecastStep      = f.ForecastStep,
            PredictedQuantity = f.PredictedQuantity,
            LowerBound        = f.LowerBound,
            UpperBound        = f.UpperBound
        }).ToList(),
        Metrics = e.PredictionMetrics.Where(m => m.Method == MetricMethod.XGBoost).Select(MapMetric).ToList(),
        ProductMetrics = e.PredictionMetrics.Where(m => m.Method == MetricMethod.XGBoost && m.Scope == MetricScope.Product).Select(MapMetric).ToList(),
        BaselineMetrics = e.PredictionMetrics.Where(m => m.Method == MetricMethod.NaiveWeekly).Select(MapMetric).ToList(),
        BaselineProductMetrics = e.PredictionMetrics.Where(m => m.Method == MetricMethod.NaiveWeekly && m.Scope == MetricScope.Product).Select(MapMetric).ToList()
    };

    private static PredictionMetricResponse MapMetric(PredictionMetric m) => new()
    {
        Method = m.Method,
        Scope = m.Scope,
        ProductName = m.ProductName,
        Wape = m.Wape,
        Mae = m.Mae,
        Rmse = m.Rmse,
        Mbe = m.Mbe,
        ActualTotal = m.ActualTotal,
        PredictedTotal = m.PredictedTotal,
        AbsoluteTotalError = m.AbsoluteTotalError,
        TotalErrorPercentage = m.TotalErrorPercentage,
        Mape = m.Mape,
        Rows = m.Rows,
        HistoricalAvg = m.HistoricalAvg
    };
}
