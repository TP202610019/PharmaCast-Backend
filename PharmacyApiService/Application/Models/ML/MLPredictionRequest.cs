namespace PharmacyApiService.Application.Models.ML;

/// <summary>
/// Payload sent to POST /predictions/execute on the ML Service.
/// Property names are camelCase to match the FastAPI / Pydantic schema.
/// </summary>
public class MLPredictionRequest
{
    public string PredictionExecutionId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DatasetPath { get; set; } = string.Empty;
    public int ForecastPeriod { get; set; } = 30;

    /// <summary>
    /// Dynamic field-to-column mapping sent to the ML pipeline.
    /// key = field_key (e.g. "product", "quantity", "date");
    /// value = actual column name in the user's dataset.
    /// </summary>
    public MLFieldMappings FieldMappings { get; set; } = new();
}

public class MLFieldMappings
{
    public Dictionary<string, string> Fields { get; set; } = new();
}
