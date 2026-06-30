namespace PharmacyApiService.Domain.Entities;

public class MappingField : BaseEntity
{
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;   // "sales" | "inventory"
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MappingConfigurationField> MappingConfigurationFields { get; set; } = [];
}
