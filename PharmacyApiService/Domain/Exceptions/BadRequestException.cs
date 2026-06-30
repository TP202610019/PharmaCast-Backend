using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class BadRequestException : AppException
{
    public BadRequestException(string message = "The request is invalid.")
        : base(ResponseCodes.BadRequest, HttpStatusCode.BadRequest, message) { }
}
