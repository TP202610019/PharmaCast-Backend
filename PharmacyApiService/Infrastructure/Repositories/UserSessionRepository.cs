using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class UserSessionRepository : GenericRepository<UserSession>, IUserSessionRepository
{
    public UserSessionRepository(ApplicationDbContext db) : base(db) { }

    public async Task<UserSession?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(s => s.User)
                     .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken, ct);

    public async Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await _set.Where(s => s.UserId == userId && !s.IsRevoked).ToListAsync(ct);
        foreach (var session in sessions)
            session.IsRevoked = true;
    }
}
