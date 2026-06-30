using PharmacyApiService.Presentation.DTOs.Auth;

namespace PharmacyApiService.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
