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
            .InclusiveBetween(1, 365).WithMessage("Forecast period must be between 1 and 365 days.");
    }
}
