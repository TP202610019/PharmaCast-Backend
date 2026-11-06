using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PharmacyApiService.Infrastructure.Persistence;

// Design-time schema generation must never run Program or its development seeder.
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=pharmacy_design_only;Username=design_only;Password=design_only")
            .UseSnakeCaseNamingConvention().Options;
        return new ApplicationDbContext(options);
    }
}
