namespace PharmacyApiService.Infrastructure.Security.Jwt;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "PharmacyApiService";
    public string Audience { get; init; } = "PharmacyApiService";
    public int ExpirationMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 7;
}
