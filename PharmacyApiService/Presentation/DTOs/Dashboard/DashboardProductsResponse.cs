namespace PharmacyApiService.Presentation.DTOs.Dashboard;

public class DashboardProductRow
{
    public string ProductName { get; set; } = string.Empty;
    public double TotalPredictedQuantity { get; set; }
    public double AvgPredictedQuantity { get; set; }
    public string Priority { get; set; } = "low";
    public double? HistoricalAvg { get; set; }
}

public class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = [];
}
