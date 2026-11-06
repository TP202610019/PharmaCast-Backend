using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PharmacyApiService.Application.Interfaces.Repositories;

namespace PharmacyApiService.Presentation.Controllers;

[ApiController]
[Route("api/auth/recovery")]
public sealed class RecoveryController(
    IUserRepository users, IUserSessionRepository sessions, IMemoryCache cache,
    IHttpClientFactory clients, IConfiguration config, ILogger<RecoveryController> logger) : ControllerBase
{
    private const int Minutes = 10;
    private static readonly object Gate = new();
    private sealed record RecoveryChallenge(byte[] Digest, DateTimeOffset Expires, int Attempts);
    public sealed record EmailRequest([Required, EmailAddress, StringLength(254)] string Email);
    public sealed record CodeRequest([Required, EmailAddress, StringLength(254)] string Email,
        [Required, RegularExpression("^[0-9]{6}$")] string Code);
    public sealed record ResetRequest([Required, EmailAddress, StringLength(254)] string Email,
        [Required, RegularExpression("^[0-9]{6}$")] string Code,
        [Required, StringLength(72, MinimumLength = 8)] string Password);

    [HttpPost("request")]
    public async Task<IActionResult> RequestCode(EmailRequest request, CancellationToken ct)
    {
        var publicKey = config["EmailJs:PublicKey"];
        var privateKey = config["EmailJs:PrivateKey"];
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
            return StatusCode(503, new { message = "La recuperación por correo todavía no está disponible." });

        var email = request.Email.Trim().ToLowerInvariant();
        var cooldown = "recovery-cooldown:" + email;
        var ipKey = "recovery-ip:" + HttpContext.Connection.RemoteIpAddress;
        lock (Gate)
        {
            if (cache.TryGetValue(cooldown, out _)) return StatusCode(429);
            var count = cache.GetOrCreate(ipKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return 0; });
            if (count >= 10) return StatusCode(429);
            cache.Set(ipKey, count + 1, TimeSpan.FromMinutes(1));
            cache.Set(cooldown, true, TimeSpan.FromMinutes(1));
        }

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive)
            return NotFound(new { message = "No hay una cuenta activa registrada con ese correo." });

        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var challenge = new RecoveryChallenge(Digest(email, code), DateTimeOffset.UtcNow.AddMinutes(Minutes), 0);
        var key = "recovery:" + email;
        lock (Gate) cache.Set(key, challenge, challenge.Expires);
        try
        {
            using var response = await clients.CreateClient("RecoveryEmail").PostAsJsonAsync(
                "https://api.emailjs.com/api/v1.0/email/send", new
                {
                    service_id = config["EmailJs:ServiceId"] ?? "service_na46gc2",
                    template_id = config["EmailJs:TemplateId"] ?? "template_kgbr6fj",
                    user_id = publicKey,
                    accessToken = privateKey,
                    template_params = new { to_email = email, code, expires_minutes = Minutes }
                }, ct);
            if (response.IsSuccessStatusCode) return NoContent();
            logger.LogWarning("EmailJS recovery request failed with status {Status}", response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning("EmailJS recovery request did not complete");
        }

        lock (Gate)
        {
            if (cache.TryGetValue<RecoveryChallenge>(key, out var current) && ReferenceEquals(current, challenge))
                cache.Remove(key);
        }
        return StatusCode(502, new { message = "No pudimos enviar el código. Intenta de nuevo más tarde." });
    }

    [HttpPost("verify")]
    public IActionResult Verify(CodeRequest request) =>
        Check(request.Email, request.Code, consume: false) ? NoContent() : InvalidCode();

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetRequest request, CancellationToken ct)
    {
        if (!request.Password.Any(char.IsAsciiLetterUpper) || !request.Password.Any(char.IsAsciiDigit))
            return BadRequest(new { message = "Incluye una letra mayúscula y un número en la contraseña." });
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            return BadRequest(new { message = "La contraseña supera el tamaño permitido." });
        if (!Check(request.Email, request.Code, consume: true)) return InvalidCode();

        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive) return InvalidCode();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        users.Update(user);
        await users.SaveChangesAsync(ct);
        await sessions.RevokeAllUserSessionsAsync(user.Id, ct);
        await sessions.SaveChangesAsync(ct);
        return NoContent();
    }

    private IActionResult InvalidCode() => BadRequest(new { message = "El código no es válido o ha vencido." });

    private byte[] Digest(string email, string code) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(config["EmailJs:PrivateKey"] ?? ""), Encoding.UTF8.GetBytes(email + ":" + code));

    private bool Check(string email, string code, bool consume)
    {
        email = email.Trim().ToLowerInvariant();
        var key = "recovery:" + email;
        lock (Gate)
        {
            if (!cache.TryGetValue<RecoveryChallenge>(key, out var challenge) || challenge is null) return false;
            if (CryptographicOperations.FixedTimeEquals(challenge.Digest, Digest(email, code)))
            {
                if (consume) cache.Remove(key);
                return true;
            }
            if (challenge.Attempts >= 4) cache.Remove(key);
            else cache.Set(key, challenge with { Attempts = challenge.Attempts + 1 }, challenge.Expires);
            return false;
        }
    }
}
