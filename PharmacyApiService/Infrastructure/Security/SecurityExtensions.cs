using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PharmacyApiService.Application.Interfaces.Security;
using PharmacyApiService.Infrastructure.Security.Jwt;

namespace PharmacyApiService.Infrastructure.Security;

public static class SecurityExtensions
{
    public static IServiceCollection AddJwtSecurity(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));

        var settings = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings section not found.");

        if (string.IsNullOrWhiteSpace(settings.Secret))
            throw new InvalidOperationException("JWT Secret not configured.");

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IJwtErrorResolver, JwtErrorResolver>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = JwtEventsFactory.Create();
        });

        services.AddAuthorization();

        return services;
    }
}
