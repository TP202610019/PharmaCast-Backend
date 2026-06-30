using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(ResponseCodes.Forbidden, HttpStatusCode.Forbidden, message) { }
}
