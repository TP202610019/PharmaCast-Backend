using FluentValidation;
using PharmacyApiService.Presentation.DTOs.Mapping;

namespace PharmacyApiService.Presentation.Validators.Mapping;

public class CreateMappingRequestValidator : AbstractValidator<CreateMappingRequest>
{
    public CreateMappingRequestValidator()
    {
        RuleFor(x => x.ConfigurationName)
            .NotEmpty().WithMessage("Configuration name is required.")
            .MaximumLength(100).WithMessage("Configuration name cannot exceed 100 characters.");

        RuleFor(x => x.Fields)
            .NotNull().WithMessage("Fields list is required.")
            .Must(fields => fields.Count > 0).WithMessage("At least one field mapping is required.");

        RuleForEach(x => x.Fields).ChildRules(field =>
        {
            field.RuleFor(f => f.FieldKey)
                .NotEmpty().WithMessage("FieldKey cannot be empty.")
                .MaximumLength(50).WithMessage("FieldKey cannot exceed 50 characters.");

            field.RuleFor(f => f.DatasetColumnName)
                .NotEmpty().WithMessage("DatasetColumnName cannot be empty.")
                .MaximumLength(100).WithMessage("DatasetColumnName cannot exceed 100 characters.");
        });
    }
}
