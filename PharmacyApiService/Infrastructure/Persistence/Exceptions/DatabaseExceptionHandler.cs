using Microsoft.EntityFrameworkCore;
using Npgsql;
using PharmacyApiService.Domain.Exceptions;

namespace PharmacyApiService.Infrastructure.Persistence.Exceptions;

public static class DatabaseExceptionHandler
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string NotNullViolation = "23502";
    private const string CheckViolation = "23514";

    public static Exception Translate(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                UniqueViolation    => new ConflictException(ResolveUniqueViolationMessage(pgEx)),
                ForeignKeyViolation => new ConflictException("This operation references a related record that does not exist or has constraints preventing modification."),
                NotNullViolation   => new BadRequestException("A required field is missing a value."),
                CheckViolation     => new BadRequestException("The value provided violates a data integrity rule."),
                _                  => ex
            };
        }

        return ex;
    }

    private static string ResolveUniqueViolationMessage(PostgresException pgEx)
    {
        var constraint = pgEx.ConstraintName ?? string.Empty;

        if (constraint.Contains("email"))
            return "A user with this email address already exists.";

        if (constraint.Contains("configuration_name") || constraint.Contains("mapping_configuration"))
            return "A mapping configuration with this name already exists for this user.";

        if (constraint.Contains("refresh_token"))
            return "A session token conflict occurred. Please try again.";

        if (constraint.Contains("purchase_plan") && constraint.Contains("prediction_execution"))
            return "A purchase plan already exists for this prediction execution.";

        return "A record with the same unique value already exists.";
    }
}
