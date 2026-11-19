using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Presentation.DTOs.Prediction;

public class PredictionResponse
{
    public string ForecastFrequency { get; set; } = "Daily";
    public double? ConfidenceLevel { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, string>? ProductCategories { get; set; }
    public Guid Id { get; set; }
    public Guid DatasetId { get; set; }
    public int ForecastPeriod { get; set; }
    public ExecutionStatus ExecutionStatus { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TrainingPeriodStart { get; set; }
    public DateTime? TrainingPeriodEnd { get; set; }
    public DateTime? EvaluationPeriodStart { get; set; }
    public DateTime? EvaluationPeriodEnd { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDemo { get; set; }
    public bool IsRetrain { get; set; }
    public Dictionary<string, string> ArtifactPaths { get; set; } = [];
    public List<string>? Warnings { get; set; }
    public List<ForecastResultResponse> ForecastResults { get; set; } = [];
    public List<PredictionMetricResponse> Metrics { get; set; } = [];
    public List<PredictionMetricResponse> ProductMetrics { get; set; } = [];
    public List<PredictionMetricResponse> BaselineMetrics { get; set; } = [];
    public List<PredictionMetricResponse> BaselineProductMetrics { get; set; } = [];
    public IEnumerable<PredictionProductSummary> ProductSummaries => ForecastResults.GroupBy(f => f.ProductName).Select(g =>
    {
        get
        {
            var history = Metrics.Where(m => m.Method == MetricMethod.XGBoost &&
                m.Scope == MetricScope.Product && m.ProductName != null)
                .ToDictionary(m => m.ProductName!, m => m.HistoricalAvg);
            return ForecastResults.GroupBy(f => f.ProductName).Select(g =>
            {
                var total = g.Sum(f => f.PredictedQuantity);
                var days = Math.Max(ForecastPeriod, 1);
                var average = total / days;
                var historical = history.GetValueOrDefault(g.Key);
                string? trend = null;
                if (historical.HasValue && ConfidenceLevel.HasValue &&
                    g.All(f => f.LowerBound.HasValue && f.UpperBound.HasValue))
                {
                    // Compare average demand rates over the same number of days.
                    var historicalTotal = historical.Value * days;
                    trend = g.Sum(f => f.LowerBound!.Value) > historicalTotal ? "up"
                        : g.Sum(f => f.UpperBound!.Value) < historicalTotal ? "down"
                        : "steady";
                }

                return new PredictionProductSummary
                {
                    ProductName = g.Key,
                    Category = ProductCategories?.GetValueOrDefault(g.Key),
                    TotalPredicted = total,
                    AverageDailyPredicted = average,
                    HistoricalAvg = historical,
                    VariationPercent = historical > 0 ? (average - historical.Value) / historical.Value * 100 : null,
                    Trend = trend
                };
            });
        }
    }
    public DateTime CreatedAt { get; set; }
}

public class PredictionSummaryResponse
{
    public int? TotalProducts { get; set; }
    public double? TotalUnits { get; set; }
    public double? Accuracy { get; set; }
    public Guid Id { get; set; }
    public Guid DatasetId { get; set; }
    public string DatasetName { get; set; } = string.Empty;
    public int ForecastPeriod { get; set; }
    public ExecutionStatus ExecutionStatus { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PredictionProductSummary
{
    public string ProductName { get; set; } = string.Empty;
    public double TotalPredicted { get; set; }
    public string? Category { get; set; }
    public string? Trend { get; set; }
    public double AverageDailyPredicted { get; set; }
    public double? HistoricalAvg { get; set; }
    public double? VariationPercent { get; set; }
}

public class ForecastResultResponse
{
    public DateTime? PeriodEnd { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime ForecastDate { get; set; }
    public int? ForecastStep { get; set; }
    public double PredictedQuantity { get; set; }
    public double? LowerBound { get; set; }
    public double? UpperBound { get; set; }
}

public class PredictionMetricResponse
{
    public MetricMethod Method { get; set; }
    public MetricScope Scope { get; set; }
    // NULL = aggregate (overall); non-null = per-product row
    public string? ProductName { get; set; }
    public double? ActualTotal { get; set; }
    public double? PredictedTotal { get; set; }
    public double? AbsoluteTotalError { get; set; }
    public double? TotalErrorPercentage { get; set; }
    public double? VolumeAccuracy => TotalErrorPercentage.HasValue ? 100 - TotalErrorPercentage.Value : null;
    public double? Wape { get; set; }
    public double? Mae { get; set; }
    public double? Rmse { get; set; }
    public double? Mbe { get; set; }
    public double? Mape { get; set; }
    public int? Rows { get; set; }
    public double? HistoricalAvg { get; set; }
}
