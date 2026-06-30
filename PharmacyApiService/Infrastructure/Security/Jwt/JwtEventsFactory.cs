using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PharmacyApiService.Application.Interfaces.Security;
using PharmacyApiService.Shared.Constants;
using PharmacyApiService.Shared.Wrappers;

public static class JwtEventsFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JwtBearerEvents Create() => new()
    {
        OnAuthenticationFailed = context =>
        {
            // Forward the exception to OnChallenge so it can write the appropriate 401 response.
            // Writing directly here would start the response before OnChallenge fires on [Authorize]
            // endpoints, causing "StatusCode cannot be set because the response has already started."
            context.Fail(context.Exception);
            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            context.HandleResponse();

            if (context.Response.HasStarted)
                return Task.CompletedTask;

            var resolver = context.HttpContext.RequestServices
                .GetRequiredService<IJwtErrorResolver>();

            var error = context.AuthenticateFailure is not null
                ? resolver.Resolve(context.AuthenticateFailure)
                : new JwtError(ResponseCodes.Unauthorized, "Authentication is required.");

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(ApiResponse<object>.Fail(error.Code, error.Message), JsonOptions);

            return context.Response.WriteAsync(body);
        },

        OnForbidden = context =>
        {
            if (context.Response.HasStarted)
                return Task.CompletedTask;

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(ApiResponse<object>.Fail(ResponseCodes.Forbidden, "You do not have permission to perform this action."), JsonOptions);

            return context.Response.WriteAsync(body);
        }
    };
}