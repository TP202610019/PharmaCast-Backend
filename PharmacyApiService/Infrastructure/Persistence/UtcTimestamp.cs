namespace PharmacyApiService.Infrastructure.Persistence;

// Binary COPY bypasses EF value converters. Normalize instants before Npgsql writes them.
public static class UtcTimestamp
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
