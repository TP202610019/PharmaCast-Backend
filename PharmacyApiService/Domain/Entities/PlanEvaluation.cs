namespace PharmacyApiService.Domain.Entities;

public class PlanEvaluation : BaseEntity
{
    public Guid PurchasePlanId { get; set; }
    public PurchasePlan PurchasePlan { get; set; } = null!;

    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public int    TotalProducts    { get; set; }
    public bool   HasPharmacyPlan  { get; set; }
    public bool   HasPrice         { get; set; }

    // PharmaCast accuracy
    public int    PharmaCastGoodCount  { get; set; }
    public double PharmaCastGoodPct    { get; set; }
    public double PharmaCastWape       { get; set; }

    // Pharmacy plan accuracy (nullable — only when HasPharmacyPlan)
    public int?    PharmacyGoodCount  { get; set; }
    public double? PharmacyGoodPct    { get; set; }
    public double? PharmacyWape       { get; set; }

    // Sobrestock / desabasto in soles
    public double  PharmaCastTotalSobrestockSoles  { get; set; }
    public double  PharmaCastTotalDesabastoSoles   { get; set; }
    public double? PharmacyTotalSobrestockSoles    { get; set; }
    public double? PharmacyTotalDesabastoSoles     { get; set; }

    public ICollection<PlanEvaluationItem> Items { get; set; } = [];
}
