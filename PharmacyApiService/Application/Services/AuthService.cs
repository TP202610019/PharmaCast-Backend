using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Security;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Presentation.DTOs.Auth;

namespace PharmacyApiService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUserSessionRepository _sessions;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;

    public AuthService(
        IUserRepository users,
        IUserSessionRepository sessions,
        ITokenService tokens,
        IConfiguration config)
    {
        _users = users;
        _sessions = sessions;
        _tokens = tokens;
        _config = config;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("Account is disabled.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return await CreateSessionAsync(user, ipAddress, userAgent, ct);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _users.EmailExistsAsync(request.Email, ct))
            throw new ConflictException($"An account with email '{request.Email}' already exists.");

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true
        };

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        return await CreateSessionAsync(user, null, null, ct);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var principal = _tokens.GetPrincipalFromExpiredToken(request.AccessToken)
            ?? throw new UnauthorizedException("Invalid access token.");

        var session = await _sessions.GetByRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (session.IsRevoked)
            throw new UnauthorizedException("Refresh token has been revoked.");

        if (session.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        var user = session.User;
        if (!user.IsActive)
            throw new ForbiddenException("Account is disabled.");

        session.IsRevoked = true;
        _sessions.Update(session);
        await _sessions.SaveChangesAsync(ct);

        return await CreateSessionAsync(user, ipAddress, userAgent, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var session = await _sessions.GetByRefreshTokenAsync(refreshToken, ct);
        if (session is null || session.IsRevoked) return;

        session.IsRevoked = true;
        _sessions.Update(session);
        await _sessions.SaveChangesAsync(ct);
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        return MapUserToProfile(user);
    }

    private async Task<AuthResponse> CreateSessionAsync(User user, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var refreshTokenDays = int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7");
        var accessToken = _tokens.GenerateAccessToken(user);
        var refreshToken = _tokens.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpirationMinutes"] ?? "60"));

        var session = new UserSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _sessions.AddAsync(session, ct);
        await _sessions.SaveChangesAsync(ct);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapUserToProfile(user)
        };
    }

    private static UserProfileResponse MapUserToProfile(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
