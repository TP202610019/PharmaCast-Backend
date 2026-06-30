using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Infrastructure.Persistence;

namespace PharmacyApiService.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext db) : base(db) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await _set.AnyAsync(u => u.Email == email.ToLower(), ct);
}
