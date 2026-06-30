using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Application.Interfaces.Repositories;

public interface IUserSessionRepository : IGenericRepository<UserSession>
{
    Task<UserSession?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default);
}
