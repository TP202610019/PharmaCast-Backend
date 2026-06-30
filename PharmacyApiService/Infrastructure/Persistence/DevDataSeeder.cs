using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence;

public static class DevDataSeeder
{
    public const string TestEmail    = "test@pharmacy.dev";
    public const string TestPassword = "Test1234!";
    public const string TestFullName = "Test User";

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var users  = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                         .CreateLogger(nameof(DevDataSeeder));

        if (await users.EmailExistsAsync(TestEmail))
        {
            logger.LogInformation("Dev seed: test user already exists ({Email}).", TestEmail);
            return;
        }

        var user = new User
        {
            FullName     = TestFullName,
            Email        = TestEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
            IsActive     = true
        };

        await users.AddAsync(user);
        await users.SaveChangesAsync();

        logger.LogInformation(
            "Dev seed: test user created — Email: {Email} | Password: {Password}",
            TestEmail, TestPassword);
    }
}
