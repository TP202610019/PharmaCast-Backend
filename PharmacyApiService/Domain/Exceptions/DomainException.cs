using System.Net;

namespace PharmacyApiService.Domain.Exceptions;

/// <summary>
/// Generic fallback exception. Prefer using typed exceptions (NotFoundException, ConflictException, etc.) instead.
/// </summary>
public sealed class DomainException : AppException
{
    public DomainException(string code, string message)
        : base(code, HttpStatusCode.BadRequest, message) { }
}
