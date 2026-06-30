using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Presentation.DTOs.Dashboard;

public class DashboardSummaryResponse
{
    public Guid? LatestPredictionId { get; set; }
    public DateTime? LatestPredictionDate { get; set; }
    public int? ForecastPeriod { get; set; }
    public ExecutionStatus? LatestStatus { get; set; }
    public double Accuracy { get; set; }
    public int TotalProducts { get; set; }
    public int CriticalCount { get; set; }
    public double TotalForecastedUnits { get; set; }
    public List<string> ProductNames { get; set; } = [];
    /// <summary>Column name mapped to "product" field_key in the saved mapping config.</summary>
    public string? SalesProductColumn { get; set; }
    /// <summary>Column name mapped to "quantity" field_key in the saved mapping config.</summary>
    public string? SalesQuantityColumn { get; set; }
}
