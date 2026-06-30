namespace PharmacyApiService.Domain.Entities;

public class PlanEvaluationItem : BaseEntity
{
    public Guid PlanEvaluationId { get; set; }
    public PlanEvaluation PlanEvaluation { get; set; } = null!;

    public string ProductName    { get; set; } = string.Empty;
    public double ActualSold     { get; set; }
    public double PharmaCastPlan { get; set; }
    public double? PharmacyPlan  { get; set; }

    public string  PharmaCastAccuracy { get; set; } = string.Empty; // "good" | "ok" | "poor"
    public string? PharmacyAccuracy   { get; set; }

    public double  Wape         { get; set; }
    public double? PharmacyWape { get; set; }

    public double  SobrestockSoles          { get; set; }
    public double  DesabastoSoles           { get; set; }
    public double? PharmacySobrestockSoles  { get; set; }
    public double? PharmacyDesabastoSoles   { get; set; }
}
