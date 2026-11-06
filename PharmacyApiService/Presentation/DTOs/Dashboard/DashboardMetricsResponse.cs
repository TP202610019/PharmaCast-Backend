using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Presentation.DTOs.Dashboard;

public class DashboardMetricsResponse
{
    public double? AverageAccuracy { get; set; }
    public int TotalDatasets { get; set; }
    public int TotalPredictions { get; set; }
    public int CompletedPredictions { get; set; }
    public int PendingPredictions { get; set; }

    public List<RecentExecutionSummary> RecentExecutions { get; set; } = [];
}

public class RecentExecutionSummary
{
    public Guid Id { get; set; }
    public string DatasetName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
