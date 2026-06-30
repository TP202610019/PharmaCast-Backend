namespace PharmacyApiService.Application.Interfaces.Security;

public interface IJwtErrorResolver
{
    JwtError Resolve(Exception? exception);
}
