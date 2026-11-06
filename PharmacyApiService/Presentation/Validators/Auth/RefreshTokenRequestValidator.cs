using FluentValidation;
using PharmacyApiService.Presentation.DTOs.Auth;

namespace PharmacyApiService.Presentation.Validators.Auth;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Falta el token de acceso.");
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Falta el token de renovación.");
    }
}
