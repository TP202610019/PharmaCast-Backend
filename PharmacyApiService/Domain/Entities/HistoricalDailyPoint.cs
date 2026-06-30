namespace PharmacyApiService.Domain.Entities;

public class HistoricalDailyPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PredictionExecutionId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Quantity { get; set; }

    public PredictionExecution PredictionExecution { get; set; } = null!;
}
