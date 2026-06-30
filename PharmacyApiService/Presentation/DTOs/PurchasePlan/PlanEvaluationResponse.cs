namespace PharmacyApiService.Presentation.DTOs.PurchasePlan;

public class PlanEvaluationItemResponse
{
    public string ProductName { get; set; } = string.Empty;
    public double ActualSold { get; set; }
    public double PharmaCastPlan { get; set; }
    public double? PharmacyPlan { get; set; }
    public string PharmaCastAccuracy { get; set; } = string.Empty;
    public string? PharmacyAccuracy { get; set; }
    public double Wape { get; set; }
    public double? PharmacyWape { get; set; }
    public double SobrestockSoles { get; set; }
    public double DesabastoSoles { get; set; }
    public double? PharmacySobrestockSoles { get; set; }
    public double? PharmacyDesabastoSoles { get; set; }
}

public class PlanEvaluationSummaryResponse
{
    public int TotalProducts { get; set; }
    public int PharmaCastGoodCount { get; set; }
    public int PharmacyGoodCount { get; set; }
    public bool HasPharmacyPlan { get; set; }
    public bool HasPrice { get; set; }
    public double PharmaCastGoodPct { get; set; }
    public double PharmacyGoodPct { get; set; }
    public double PharmaCastWape { get; set; }
    public double? PharmacyWape { get; set; }
    public double PharmaCastTotalSobrestockSoles { get; set; }
    public double PharmaCastTotalDesabastoSoles { get; set; }
    public double? PharmacyTotalSobrestockSoles { get; set; }
    public double? PharmacyTotalDesabastoSoles { get; set; }
}

public class PlanEvaluationResponse
{
    public string PlanId { get; set; } = string.Empty;
    public PlanEvaluationSummaryResponse Summary { get; set; } = new();
    public List<PlanEvaluationItemResponse> Items { get; set; } = [];
}
