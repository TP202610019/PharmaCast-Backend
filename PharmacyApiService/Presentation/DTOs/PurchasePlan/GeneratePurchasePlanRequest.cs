namespace PharmacyApiService.Presentation.DTOs.PurchasePlan;

public class GeneratePurchasePlanRequest
{
    public Guid PredictionExecutionId { get; set; }
    public string? PlanName { get; set; }
    public Guid? InventoryDatasetId { get; set; }
    public Dictionary<string, string>? InventoryFieldMappings { get; set; }
}
