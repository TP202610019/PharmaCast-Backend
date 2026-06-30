namespace PharmacyApiService.Presentation.DTOs.Mapping;

public class CreateMappingRequest
{
    public string ConfigurationName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<MappingFieldEntry> Fields { get; set; } = [];
}

public class MappingFieldEntry
{
    public string FieldKey { get; set; } = string.Empty;
    public string DatasetColumnName { get; set; } = string.Empty;
}
