using System.Security.Claims;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Shared.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppConstants.Claims.UserId);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue(AppConstants.Claims.Email) ?? string.Empty;

    public static string GetFullName(this ClaimsPrincipal principal)
        => principal.FindFirstValue(AppConstants.Claims.FullName) ?? string.Empty;

}
