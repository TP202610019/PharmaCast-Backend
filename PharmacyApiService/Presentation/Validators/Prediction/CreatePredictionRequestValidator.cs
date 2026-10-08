using FluentValidation;
using PharmacyApiService.Presentation.DTOs.Prediction;

namespace PharmacyApiService.Presentation.Validators.Prediction;

public class CreatePredictionRequestValidator : AbstractValidator<CreatePredictionRequest>
{
    public CreatePredictionRequestValidator()
    {
        RuleFor(x => x.DatasetId)
            .NotEmpty().WithMessage("Dataset ID is required.");

        RuleFor(x => x.ForecastPeriod)
            .InclusiveBetween(30, 180).WithMessage("Forecast period must be between 30 and 180 days.");
    }
}
