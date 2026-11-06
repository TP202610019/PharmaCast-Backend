namespace PharmacyApiService.Domain.Entities;

public class HistoricalDailyPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Quantity { get; set; }

    public Dataset Dataset { get; set; } = null!;
}
