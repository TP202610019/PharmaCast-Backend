namespace PharmacyApiService.Shared;

public static class DemoAccountPolicy
{
    public static bool IsDemoEmail(string? email, IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(email) && string.Equals(email.Trim(),
            configuration["DemoAccount:Email"] ?? "test@pharmacy.dev", StringComparison.OrdinalIgnoreCase);
}
