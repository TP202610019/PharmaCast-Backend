namespace PharmacyApiService.Presentation.DTOs.Prediction;

public class CreatePredictionRequest
{
    public Guid DatasetId { get; set; }
    public int ForecastPeriod { get; set; } = 30;
    public Guid? MappingConfigurationId { get; set; }
}
