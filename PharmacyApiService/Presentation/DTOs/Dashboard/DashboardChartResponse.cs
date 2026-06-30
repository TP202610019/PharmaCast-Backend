namespace PharmacyApiService.Presentation.DTOs.Dashboard;

public class DashboardChartPoint
{
    /// <summary>ISO date string in "yyyy-MM-dd" format.</summary>
    public string Date { get; set; } = string.Empty;
    public double Predicted { get; set; }
    public double? Upper { get; set; }
    public double? Lower { get; set; }
}

public class DashboardHistoricalPoint
{
    public string Date { get; set; } = string.Empty;
    public double Quantity { get; set; }
}

public class DashboardChartResponse
{
    public string ProductName { get; set; } = string.Empty;
    public List<DashboardChartPoint> Points { get; set; } = [];
    public List<DashboardHistoricalPoint> HistoricalPoints { get; set; } = [];
}
