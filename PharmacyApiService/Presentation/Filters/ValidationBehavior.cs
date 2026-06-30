using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using DomainValidationException = PharmacyApiService.Domain.Exceptions.ValidationException;

namespace PharmacyApiService.Presentation.Filters;

public class ValidationBehavior : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var errors = new Dictionary<string, string[]>();

        foreach (var (_, argument) in context.ActionArguments)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid) continue;

            foreach (var group in result.Errors.GroupBy(e => e.PropertyName))
                errors[group.Key] = [.. group.Select(e => e.ErrorMessage)];
        }

        if (errors.Count > 0)
            throw new DomainValidationException(errors);

        await next();
    }
}
