using Microsoft.IdentityModel.Tokens;
using PharmacyApiService.Application.Interfaces.Security;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Infrastructure.Security.Jwt;

public class JwtErrorResolver : IJwtErrorResolver
{
    public JwtError Resolve(Exception? exception) => exception switch
    {
        SecurityTokenExpiredException          => new(ResponseCodes.TokenExpired,  "The access token has expired."),
        SecurityTokenInvalidSignatureException => new(ResponseCodes.InvalidToken,  "The token signature is invalid."),
        SecurityTokenInvalidLifetimeException  => new(ResponseCodes.InvalidToken,  "The token has an invalid lifetime."),
        SecurityTokenException                 => new(ResponseCodes.InvalidToken,  "The token is invalid."),
        _                                      => new(ResponseCodes.Unauthorized,  "Authentication is required.")
    };
}
