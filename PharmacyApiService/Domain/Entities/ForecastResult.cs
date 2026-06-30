namespace PharmacyApiService.Domain.Entities;

public class ForecastResult : BaseEntity
{
    public Guid PredictionExecutionId { get; set; }
    public PredictionExecution PredictionExecution { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;
    public DateTime ForecastDate { get; set; }
    public double PredictedQuantity { get; set; }
    public double? LowerBound { get; set; }
    public double? UpperBound { get; set; }
}
