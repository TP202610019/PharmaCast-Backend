using System.Security.Claims;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Security;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
