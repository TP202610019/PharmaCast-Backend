using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Domain.Entities;

public class Dataset : BaseEntity
{
    public Guid UploadedBy { get; set; }
    public User User { get; set; } = null!;

    public Guid? MappingConfigurationId { get; set; }
    public MappingConfiguration? MappingConfiguration { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public string? BlobContainer { get; set; }
    public FileType FileType { get; set; }
    public DatasetStage DatasetStage { get; set; } = DatasetStage.Temporary;
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Pending;
    public int? RowsCount { get; set; }

    public ICollection<PredictionExecution> PredictionExecutions { get; set; } = [];
}
