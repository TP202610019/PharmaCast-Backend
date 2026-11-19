namespace PharmacyApiService.Presentation.DTOs.PurchasePlan;

public class PurchasePlanResponse
{
    public Guid Id { get; set; }
    public Guid PredictionExecutionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<PurchasePlanItemResponse> Items { get; set; } = [];
    public decimal? TotalEstimatedCost { get; set; }
    public int ItemsMissingPurchaseCost { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchasePlanItemResponse
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double RecommendedQuantity { get; set; }
    public double? DemandPredicted { get; set; }
    public double? StockActual { get; set; }
    public string? Priority { get; set; }
    public double? ToPurchase { get; set; }
    public string? Category { get; set; }
    public string? Supplier { get; set; }
    public double? EstimatedCost { get; set; }
    public decimal? UnitPurchaseCost { get; set; }
}
