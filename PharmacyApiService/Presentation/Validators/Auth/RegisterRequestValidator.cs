using FluentValidation;
using PharmacyApiService.Presentation.DTOs.Auth;

namespace PharmacyApiService.Presentation.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ingresa tu nombre completo.")
            .MaximumLength(150).WithMessage("El nombre admite hasta 150 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Ingresa tu correo.")
            .EmailAddress().WithMessage("Formato de correo no válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Ingresa tu contraseña.")
            .MinimumLength(8).WithMessage("Usa al menos 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("Incluye una mayúscula.")
            .Matches(@"[0-9]").WithMessage("Incluye un número.");
    }
}
