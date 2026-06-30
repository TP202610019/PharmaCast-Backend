using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string resource, object id)
        : base(ResponseCodes.NotFound, HttpStatusCode.NotFound,
               $"{resource} with identifier '{id}' was not found.") { }

    public NotFoundException(string message)
        : base(ResponseCodes.NotFound, HttpStatusCode.NotFound, message) { }
}
