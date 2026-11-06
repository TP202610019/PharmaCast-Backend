using System.ComponentModel.DataAnnotations;

namespace PharmacyApiService.Presentation.DTOs.Dataset;

public class ConfirmDatasetRequest
{
    [Required]
    public Guid MappingConfigurationId { get; set; }

    [RegularExpression("^(sales|inventory)$")]
    public string FieldType { get; set; } = "sales";
}
