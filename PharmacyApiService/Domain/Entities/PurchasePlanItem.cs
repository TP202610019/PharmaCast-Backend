namespace PharmacyApiService.Domain.Entities;

public class PurchasePlanItem : BaseEntity
{
    public Guid PurchasePlanId { get; set; }
    public PurchasePlan PurchasePlan { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;
    public double RecommendedQuantity { get; set; }
    public double? DemandPredicted { get; set; }
    public double? StockActual { get; set; }
    public double? ToPurchase { get; set; }
    public string? Category { get; set; }
    public string? Supplier { get; set; }
    public double? EstimatedCost { get; set; }
}
