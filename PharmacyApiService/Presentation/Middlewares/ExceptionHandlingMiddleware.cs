using System.Text.Json;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Shared.Constants;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(exception, "Response already started; cannot write exception response: {Message}", exception.Message);
            return;
        }

        var (statusCode, response) = BuildResponse(exception);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private (int StatusCode, ApiResponse<object> Response) BuildResponse(Exception exception)
    {
        if (exception is AppException appEx)
        {
            _logger.LogWarning(exception, "Handled exception [{Code}] - {Message}", appEx.Code, appEx.Message);
            return (
                (int)appEx.StatusCode,
                ApiResponse<object>.Fail(appEx.Code, appEx.Message, appEx.Errors)
            );
        }

        // Build a chain of all exception messages for diagnostics
        var chain = BuildExceptionChain(exception);
        _logger.LogError(exception, "Unhandled exception [{ExType}]: {Chain}", exception.GetType().Name, chain);

        // In Development, surface the real exception details so failures are immediately visible
        var message = _env.IsDevelopment()
            ? $"[{exception.GetType().Name}] {chain}"
            : "An unexpected error occurred.";

        return (
            StatusCodes.Status500InternalServerError,
            ApiResponse<object>.Fail(ResponseCodes.InternalError, message)
        );
    }

    private static string BuildExceptionChain(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current is not null)
        {
            parts.Add($"[{current.GetType().Name}] {current.Message}");
            current = current.InnerException;
        }
        return string.Join(" → Inner: ", parts);
    }
}
