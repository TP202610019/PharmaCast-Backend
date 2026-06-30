using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Presentation.DTOs.Auth;
using PharmacyApiService.Shared.Extensions;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await _auth.LoginAsync(request, ip, ua, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Login successful."));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return Created(string.Empty, ApiResponse<AuthResponse>.Ok(result, "Account created successfully."));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await _auth.RefreshTokenAsync(request, ip, ua, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Token refreshed."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return Ok(ApiResponse.Ok("Logged out successfully."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _auth.GetProfileAsync(userId, ct);
        return Ok(ApiResponse<UserProfileResponse>.Ok(result));
    }
}
