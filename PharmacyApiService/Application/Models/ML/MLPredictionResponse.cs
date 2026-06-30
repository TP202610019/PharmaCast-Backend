namespace PharmacyApiService.Application.Models.ML;

/// <summary>
/// Response from POST /api/predictions/execute on the ML Service.
/// Matches the Pydantic PredictionExecutionResponse schema (camelCase JSON deserialized here).
/// </summary>
public class MLPredictionResponse
{
    public string PredictionExecutionId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;

    /// <summary>"Completed" | "Failed" | "Processing"</summary>
    public string Status { get; set; } = string.Empty;

    public bool IsRetrain { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }

    /// <summary>Aggregate evaluation metrics over all products.</summary>
    public MLExecutionMetrics Metrics { get; set; } = new();

    /// <summary>Per-product evaluation metrics.</summary>
    public List<MLProductMetric> ProductMetrics { get; set; } = [];

    /// <summary>Forecast entries: one row per (product, date) pair.</summary>
    public List<MLForecastEntry> Forecast { get; set; } = [];

    /// <summary>Paths to stored artifacts (for auditing / debugging).</summary>
    public Dictionary<string, string> ArtifactPaths { get; set; } = [];

    public string? ErrorMessage { get; set; }

    public List<MLHistoricalEntry> HistoricalPoints { get; set; } = [];

    public bool Success => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
}

public class MLExecutionMetrics
{
    public double? Wape { get; set; }
    public double? Rmse { get; set; }
    public double? Mae { get; set; }
    public double? Mape { get; set; }
}

public class MLProductMetric
{
    public string ProductId { get; set; } = string.Empty;
    public double? Wape { get; set; }
    public double? Rmse { get; set; }
    public double? Mae { get; set; }
    public double? Mape { get; set; }
    public int Rows { get; set; }
    public double? HistoricalAvg { get; set; }
}

public class MLHistoricalEntry
{
    public string ProductId { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public double Quantity { get; set; }
}

public class MLForecastEntry
{
    public string ProductId { get; set; } = string.Empty;

    /// <summary>ISO date string, e.g. "2025-06-01".</summary>
    public string Date { get; set; } = string.Empty;

    public int ForecastStep { get; set; }
    public double PredictedQuantity { get; set; }
}

