namespace PharmacyApiService.Domain.Entities;

public class MappingConfiguration : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string ConfigurationName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public ICollection<Dataset> Datasets { get; set; } = [];
    public ICollection<MappingConfigurationField> Fields { get; set; } = [];
}
