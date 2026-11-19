using System.Security.Claims;
using PharmacyApiService.Shared;
using PharmacyApiService.Shared.Constants;
using PharmacyApiService.Shared.Wrappers;

namespace PharmacyApiService.Presentation.Middlewares;

public sealed class DemoReadOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var email = context.User.FindFirst(AppConstants.Claims.Email)?.Value
            ?? context.User.FindFirst(ClaimTypes.Email)?.Value;
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        var safe = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method);
        var sessionAction = new[] { "/api/auth/login", "/api/auth/register", "/api/auth/refresh", "/api/auth/logout" }
            .Contains(path, StringComparer.OrdinalIgnoreCase);
        if (context.User.Identity?.IsAuthenticated == true && DemoAccountPolicy.IsDemoEmail(email, configuration)
            && !safe && !sessionAction)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail("DEMO_READ_ONLY",
                "La cuenta demo solo permite consultar los análisis de ejemplo."));
            return;
        }
        await next(context);
    }
}
