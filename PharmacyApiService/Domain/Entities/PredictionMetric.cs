namespace PharmacyApiService.Domain.Entities;

public class PredictionMetric : BaseEntity
{
    public Guid PredictionExecutionId { get; set; }
    public PredictionExecution PredictionExecution { get; set; } = null!;

    // NULL = aggregate (overall); non-null = per-product row
    public string? ProductName { get; set; }

    public double? Wape { get; set; }
    public double? Rmse { get; set; }
    public double? Mae { get; set; }
    public double? Mape { get; set; }
    public int? Rows { get; set; }
    public double? HistoricalAvg { get; set; }
}
