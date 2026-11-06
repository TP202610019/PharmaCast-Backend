using System.Net;
using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Domain.Exceptions;

public class ValidationException : AppException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ResponseCodes.ValidationError, HttpStatusCode.UnprocessableEntity,
               errors.Values.SelectMany(messages => messages).FirstOrDefault()
                   ?? "One or more validation errors occurred.", errors) { }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] }) { }
}
