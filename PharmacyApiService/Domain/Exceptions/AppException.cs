using System.Net;

namespace PharmacyApiService.Domain.Exceptions;

public abstract class AppException : Exception
{
    public string Code { get; }
    public HttpStatusCode StatusCode { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    protected AppException(string code, HttpStatusCode statusCode, string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors;
    }
}
