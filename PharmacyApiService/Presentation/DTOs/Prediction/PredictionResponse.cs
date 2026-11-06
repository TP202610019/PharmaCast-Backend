using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Presentation.DTOs.Prediction;

public class PredictionResponse
{
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
        var average = g.Average(f => f.PredictedQuantity);
        var historical = Metrics.FirstOrDefault(m => m.Method == MetricMethod.XGBoost
            && m.Scope == MetricScope.Product && m.ProductName == g.Key)?.HistoricalAvg;
        return new PredictionProductSummary {
            ProductName = g.Key, TotalPredicted = g.Sum(f => f.PredictedQuantity),
            AverageDailyPredicted = average, HistoricalAvg = historical,
            VariationPercent = historical > 0 ? (average - historical.Value) / historical.Value * 100 : null
        };
    });
    public DateTime CreatedAt { get; set; }
}

public class PredictionSummaryResponse
{
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
    public double AverageDailyPredicted { get; set; }
    public double? HistoricalAvg { get; set; }
    public double? VariationPercent { get; set; }
}

public class ForecastResultResponse
{
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
