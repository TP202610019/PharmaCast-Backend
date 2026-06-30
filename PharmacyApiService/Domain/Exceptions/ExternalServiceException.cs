using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class ExternalServiceException : AppException
{
    public ExternalServiceException(string serviceName, string? detail = null)
        : base(ResponseCodes.ExternalServiceError, HttpStatusCode.ServiceUnavailable,
               BuildMessage(serviceName, detail)) { }

    public ExternalServiceException(string message)
        : base(ResponseCodes.ExternalServiceError, HttpStatusCode.ServiceUnavailable, message) { }

    private static string BuildMessage(string serviceName, string? detail) =>
        detail is null
            ? $"The external service '{serviceName}' is currently unavailable."
            : $"The external service '{serviceName}' is currently unavailable. {detail}";
}
