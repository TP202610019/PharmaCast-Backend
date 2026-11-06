using FluentValidation;
using PharmacyApiService.Presentation.DTOs.Auth;

namespace PharmacyApiService.Presentation.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Ingresa tu correo.")
            .EmailAddress().WithMessage("Formato de correo no válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Ingresa tu contraseña.")
            .MinimumLength(6).WithMessage("Usa al menos 6 caracteres.");
    }
}
