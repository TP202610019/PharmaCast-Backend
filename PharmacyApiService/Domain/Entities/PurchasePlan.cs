namespace PharmacyApiService.Domain.Entities;

public class PurchasePlan : BaseEntity
{
    public Guid PredictionExecutionId { get; set; }
    public PredictionExecution PredictionExecution { get; set; } = null!;

    public string PlanName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchasePlanItem>  Items       { get; set; } = [];
    public ICollection<PlanEvaluation>    Evaluations { get; set; } = [];
}
