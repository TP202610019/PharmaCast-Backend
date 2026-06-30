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
    public string? ErrorMessage { get; set; }
    public List<ForecastResultResponse> ForecastResults { get; set; } = [];
    public List<PredictionMetricResponse> Metrics { get; set; } = [];
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

public class ForecastResultResponse
{
    public string ProductName { get; set; } = string.Empty;
    public DateTime ForecastDate { get; set; }
    public double PredictedQuantity { get; set; }
    public double? LowerBound { get; set; }
    public double? UpperBound { get; set; }
}

public class PredictionMetricResponse
{
    // NULL = aggregate (overall); non-null = per-product row
    public string? ProductName { get; set; }
    public double? Wape { get; set; }
    public double? Rmse { get; set; }
    public double? Mae { get; set; }
    public double? Mape { get; set; }
    public int? Rows { get; set; }
    public double? HistoricalAvg { get; set; }
}
