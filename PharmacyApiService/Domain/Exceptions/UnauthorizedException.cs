using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication is required.")
        : base(ResponseCodes.Unauthorized, HttpStatusCode.Unauthorized, message) { }
}
