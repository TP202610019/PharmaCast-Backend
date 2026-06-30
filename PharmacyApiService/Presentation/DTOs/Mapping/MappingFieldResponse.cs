namespace PharmacyApiService.Presentation.DTOs.Mapping;

public class MappingFieldResponse
{
    public Guid Id { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
}
