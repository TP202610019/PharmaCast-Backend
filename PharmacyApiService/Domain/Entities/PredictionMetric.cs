using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Domain.Entities;

public class PredictionMetric : BaseEntity
{
    public Guid PredictionExecutionId { get; set; }
    public PredictionExecution PredictionExecution { get; set; } = null!;

    // NULL = aggregate (overall); non-null = per-product row
    public string? ProductName { get; set; }
    public MetricMethod Method { get; set; } = MetricMethod.XGBoost;
    public MetricScope Scope { get; set; } = MetricScope.Overall;

    public double? ActualTotal { get; set; }
    public double? PredictedTotal { get; set; }
    public double? AbsoluteTotalError { get; set; }
    public double? TotalErrorPercentage { get; set; }
    public double? Wape { get; set; }
    public double? Mae { get; set; }
    public double? Rmse { get; set; }
    public double? Mbe { get; set; }
    public double? Mape { get; set; }
    public int? Rows { get; set; }
    public double? HistoricalAvg { get; set; }
}
