using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(ResponseCodes.Conflict, HttpStatusCode.Conflict, message) { }
}
