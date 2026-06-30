using FluentValidation;
using PharmacyApiService.Presentation.DTOs.PurchasePlan;

namespace PharmacyApiService.Presentation.Validators.PurchasePlan;

public class GeneratePurchasePlanRequestValidator : AbstractValidator<GeneratePurchasePlanRequest>
{
    public GeneratePurchasePlanRequestValidator()
    {
        RuleFor(x => x.PredictionExecutionId)
            .NotEmpty().WithMessage("Prediction execution ID is required.");
    }
}
