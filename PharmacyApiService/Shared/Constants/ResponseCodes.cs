namespace PharmacyApiService.Shared.Constants;

public static class ResponseCodes
{
    public const string Success = "SUCCESS";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string RefreshTokenExpired = "REFRESH_TOKEN_EXPIRED";
    public const string ExternalServiceError = "EXTERNAL_SERVICE_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string BadRequest = "BAD_REQUEST";
}
