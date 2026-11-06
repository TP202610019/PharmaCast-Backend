using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Domain.Enums;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Infrastructure.Persistence;
using PharmacyApiService.Presentation.DTOs.Dashboard;

namespace PharmacyApiService.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardMetricsResponse> GetMetricsAsync(Guid userId, CancellationToken ct = default)
    {
        var totalDatasets = await _db.Datasets.CountAsync(d => d.UploadedBy == userId, ct);
        var totalPredictions = await _db.PredictionExecutions.CountAsync(p => p.ExecutedBy == userId, ct);
        var completedPredictions = await _db.PredictionExecutions.CountAsync(p => p.ExecutedBy == userId && p.ExecutionStatus == ExecutionStatus.Completed, ct);
        var pendingPredictions = await _db.PredictionExecutions.CountAsync(p => p.ExecutedBy == userId && (p.ExecutionStatus == ExecutionStatus.Pending || p.ExecutionStatus == ExecutionStatus.Processing), ct);
        var recentExecutions = await _db.PredictionExecutions
            .Include(p => p.Dataset)
            .Where(p => p.ExecutedBy == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new RecentExecutionSummary
            {
                Id = p.Id,
                DatasetName = p.Dataset.OriginalFileName,
                Status = p.ExecutionStatus,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return new DashboardMetricsResponse
        {
            TotalDatasets = totalDatasets,
            TotalPredictions = totalPredictions,
            CompletedPredictions = completedPredictions,
            PendingPredictions = pendingPredictions,
            RecentExecutions = recentExecutions
        };
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var latest = await _db.PredictionExecutions
            .Where(p => p.ExecutedBy == userId && p.ExecutionStatus == ExecutionStatus.Completed)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.CreatedAt, p.ForecastPeriod, p.ExecutionStatus })
            .FirstOrDefaultAsync(ct);

        if (latest is null)
            return new DashboardSummaryResponse();

        return await BuildSummaryResponseAsync(latest.Id, latest.CreatedAt, latest.ForecastPeriod, latest.ExecutionStatus, ct);
    }

    public async Task<DashboardSummaryResponse> GetPredictionSummaryAsync(Guid predictionId, Guid userId, CancellationToken ct = default)
    {
        var execution = await _db.PredictionExecutions
            .Where(p => p.Id == predictionId && p.ExecutedBy == userId)
            .Select(p => new { p.Id, p.CreatedAt, p.ForecastPeriod, p.ExecutionStatus, p.DatasetId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("PredictionExecution", predictionId);

        // Resolve field mapping columns saved for this dataset
        var mappingFields = await _db.MappingConfigurations
            .Where(mc => mc.Datasets.Any(d => d.Id == execution.DatasetId))
            .SelectMany(mc => mc.Fields)
            .Where(f => f.MappingField != null && (f.MappingField.FieldKey == "product_id" || f.MappingField.FieldKey == "product" || f.MappingField.FieldKey == "quantity"))
            .Select(f => new { f.MappingField!.FieldKey, f.DatasetColumnName })
            .ToListAsync(ct);

        var salesProductCol  = mappingFields.FirstOrDefault(f => f.FieldKey == "product_id")?.DatasetColumnName
            ?? mappingFields.FirstOrDefault(f => f.FieldKey == "product")?.DatasetColumnName;
        var salesQuantityCol = mappingFields.FirstOrDefault(f => f.FieldKey == "quantity")?.DatasetColumnName;

        var summary = await BuildSummaryResponseAsync(execution.Id, execution.CreatedAt, execution.ForecastPeriod, execution.ExecutionStatus, ct);
        summary.SalesProductColumn  = salesProductCol;
        summary.SalesQuantityColumn = salesQuantityCol;
        return summary;
    }

    private async Task<DashboardSummaryResponse> BuildSummaryResponseAsync(
        Guid predictionId, DateTime createdAt, int forecastPeriod, ExecutionStatus status, CancellationToken ct)
    {
        var wapeValue = await _db.PredictionMetrics
            .Where(m => m.PredictionExecutionId == predictionId
                     && m.Method == MetricMethod.XGBoost && m.Scope == MetricScope.Overall)
            .Select(m => m.Wape)
            .FirstOrDefaultAsync(ct);

        var accuracy = wapeValue.HasValue
            ? Math.Round(Math.Clamp(100.0 - wapeValue.Value, 0.0, 100.0), 1)
            : 0.0;

        var productStats = await _db.ForecastResults
            .Where(f => f.PredictionExecutionId == predictionId)
            .GroupBy(f => f.ProductName)
            .Select(g => new
            {
                Name  = g.Key,
                Total = g.Sum(f => f.PredictedQuantity),
                Avg   = g.Average(f => f.PredictedQuantity),
            })
            .ToListAsync(ct);

        var productNames       = productStats.Select(p => p.Name).OrderBy(n => n).ToList();
        var priorities         = BuildPriorityMap(productStats.Select(p => (p.Name, p.Total)));
        var criticalCount      = priorities.Values.Count(priority => priority == "critical");
        var totalForecastedUnits = productStats.Sum(p => p.Total);

        return new DashboardSummaryResponse
        {
            LatestPredictionId   = predictionId,
            LatestPredictionDate = createdAt,
            ForecastPeriod       = forecastPeriod,
            LatestStatus         = status,
            Accuracy             = accuracy,
            TotalProducts        = productNames.Count,
            CriticalCount        = criticalCount,
            TotalForecastedUnits = Math.Round(totalForecastedUnits),
            ProductNames         = productNames
        };
    }

    public async Task<PagedResult<DashboardProductRow>> GetProductsAsync(
        Guid userId, Guid predictionId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var executionExists = await _db.PredictionExecutions
            .AnyAsync(p => p.Id == predictionId && p.ExecutedBy == userId, ct);

        if (!executionExists)
            throw new NotFoundException("PredictionExecution", predictionId);

        var productStats = await _db.ForecastResults
            .Where(f => f.PredictionExecutionId == predictionId)
            .GroupBy(f => f.ProductName)
            .Select(g => new
            {
                ProductName = g.Key,
                TotalPredicted = g.Sum(f => f.PredictedQuantity),
                AvgPredicted = g.Average(f => f.PredictedQuantity),
            })
            .ToListAsync(ct);

        var priorities = BuildPriorityMap(productStats.Select(p => (p.ProductName, p.TotalPredicted)));
        var filtered = productStats.Where(p => p.TotalPredicted > 0);
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(p => p.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase));

        var totalCount = filtered.Count();
        var rows = filtered
            .OrderBy(r => r.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var productNames = rows.Select(r => r.ProductName).ToList();
        var historicalAvgMap = await _db.PredictionMetrics
            .Where(m => m.PredictionExecutionId == predictionId
                     && m.Method == MetricMethod.XGBoost && m.Scope == MetricScope.Product
                     && m.ProductName != null
                     && productNames.Contains(m.ProductName)
                     && m.HistoricalAvg != null)
            .Select(m => new { m.ProductName, m.HistoricalAvg })
            .ToDictionaryAsync(m => m.ProductName!, m => m.HistoricalAvg, ct);

        var items = rows.Select(r => new DashboardProductRow
        {
            ProductName            = r.ProductName,
            TotalPredictedQuantity = Math.Round(r.TotalPredicted),
            AvgPredictedQuantity   = Math.Round(r.AvgPredicted, 1),
            Priority               = priorities.GetValueOrDefault(r.ProductName, "low"),
            HistoricalAvg          = historicalAvgMap.TryGetValue(r.ProductName, out var ha) ? ha : null,
        }).ToList();

        return new PagedResult<DashboardProductRow>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<DashboardChartResponse> GetChartAsync(
        Guid userId, Guid predictionId, string productName, CancellationToken ct = default)
    {
        var executionExists = await _db.PredictionExecutions
            .AnyAsync(p => p.Id == predictionId && p.ExecutedBy == userId, ct);

        if (!executionExists)
            throw new NotFoundException("PredictionExecution", predictionId);

        var results = await _db.ForecastResults
            .Where(f => f.PredictionExecutionId == predictionId && f.ProductName == productName)
            .OrderBy(f => f.ForecastDate)
            .Select(f => new
            {
                f.ForecastDate,
                f.PredictedQuantity,
                f.LowerBound,
                f.UpperBound
            })
            .ToListAsync(ct);

        var points = results.Select(r =>
        {
            return new DashboardChartPoint
            {
                Date = r.ForecastDate.ToString("yyyy-MM-dd"),
                Predicted = r.PredictedQuantity,
                Upper = r.UpperBound,
                Lower = r.LowerBound
            };
        }).ToList();

        var historicalPoints = await _db.HistoricalDailyPoints
            .Where(h => h.DatasetId == _db.PredictionExecutions.Where(e => e.Id == predictionId)
                .Select(e => e.DatasetId).FirstOrDefault() && h.ProductName == productName)
            .OrderBy(h => h.Date)
            .Select(h => new DashboardHistoricalPoint
            {
                Date = h.Date.ToString("yyyy-MM-dd"),
                Quantity = h.Quantity
            })
            .ToListAsync(ct);

        return new DashboardChartResponse
        {
            ProductName = productName,
            Points = points,
            HistoricalPoints = historicalPoints
        };
    }

    private static Dictionary<string, string> BuildPriorityMap(IEnumerable<(string Name, double Total)> products)
    {
        var ranked = products.Where(p => p.Total > 0)
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        var result = new Dictionary<string, string>();
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            var fraction = (double)rank / ranked.Count;
            result[ranked[rank].Name] = fraction switch
            {
                < 0.15 => "critical",
                < 0.40 => "high",
                < 0.75 => "medium",
                _ => "low"
            };
        }
        return result;
    }
}
