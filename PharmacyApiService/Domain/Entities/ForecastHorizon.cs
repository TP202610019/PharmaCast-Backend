namespace PharmacyApiService.Domain.Entities;

public class ForecastHorizon
{
    public int Days { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
