namespace PharmacyApiService.Presentation.DTOs.Mapping;

public class MappingConfigResponse
{
    public Guid Id { get; set; }
    public string ConfigurationName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<MappingFieldEntryResponse> Fields { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MappingFieldEntryResponse
{
    public string FieldKey { get; set; } = string.Empty;
    public string DatasetColumnName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
