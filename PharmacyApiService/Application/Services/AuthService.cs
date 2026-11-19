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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword))
            throw new ValidationException("currentPassword", "Ingresa tu contraseña actual.");

        var password = request.NewPassword ?? string.Empty;
        if (password.Length < 8)
            throw new ValidationException("newPassword", "La nueva contraseña debe tener al menos 8 caracteres.");
        if (System.Text.Encoding.UTF8.GetByteCount(password) > 72)
            throw new ValidationException("newPassword", "La nueva contraseña supera la longitud permitida.");
        if (!password.Any(c => c is >= 'A' and <= 'Z') || !password.Any(c => c is >= '0' and <= '9'))
            throw new ValidationException("newPassword", "Incluye una mayúscula y un número.");

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            throw new ForbiddenException("La cuenta no está activa.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("currentPassword", "La contraseña actual es incorrecta.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        _users.Update(user);
        await _users.SaveChangesAsync(ct);
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
