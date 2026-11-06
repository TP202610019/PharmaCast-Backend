using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Domain.Entities;

public class PredictionExecution : BaseEntity
{
    public Guid DatasetId { get; set; }
    public Dataset Dataset { get; set; } = null!;

    public Guid ExecutedBy { get; set; }
    public User User { get; set; } = null!;

    public int ForecastPeriod { get; set; }
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TrainingPeriodStart { get; set; }
    public DateTime? TrainingPeriodEnd { get; set; }
    public DateTime? EvaluationPeriodStart { get; set; }
    public DateTime? EvaluationPeriodEnd { get; set; }
    public int? NewProductsCount { get; set; }
    public double? NewProductsUnits { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsRetrain { get; set; }
    public Dictionary<string, string> ArtifactPaths { get; set; } = [];
    public List<string>? Warnings { get; set; }

    public ICollection<ForecastResult> ForecastResults { get; set; } = [];
    public ICollection<PredictionMetric> PredictionMetrics { get; set; } = [];
    public PurchasePlan? PurchasePlan { get; set; }

}
