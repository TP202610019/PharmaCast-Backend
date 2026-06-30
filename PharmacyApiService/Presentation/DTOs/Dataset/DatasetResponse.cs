using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Presentation.DTOs.Dataset;

public class DatasetResponse
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public FileType FileType { get; set; }
    public DatasetStage DatasetStage { get; set; }
    public UploadStatus UploadStatus { get; set; }
    public int? RowsCount { get; set; }
    public Guid? MappingConfigurationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
