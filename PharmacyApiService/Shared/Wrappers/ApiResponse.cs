using PharmacyApiService.Shared.Constants;

namespace PharmacyApiService.Shared.Wrappers;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Code = ResponseCodes.Success,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> Fail(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        Success = false,
        Code = code,
        Message = message,
        Errors = errors
    };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Code = ResponseCodes.Success,
        Message = message
    };

    public new static ApiResponse Fail(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        Success = false,
        Code = code,
        Message = message,
        Errors = errors
    };
}
