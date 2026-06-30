namespace PharmacyApiService.Domain.Entities;

public class MappingConfigurationField : BaseEntity
{
    public Guid MappingConfigurationId { get; set; }
    public MappingConfiguration MappingConfiguration { get; set; } = null!;

    public Guid MappingFieldId { get; set; }
    public MappingField MappingField { get; set; } = null!;

    public string DatasetColumnName { get; set; } = string.Empty;
}
