using HotelBookingSaaS.API.DTOs;
using System.Net;
using System.Text.Json;

namespace HotelBookingSaaS.API.Middleware;

/// <summary>Global exception handler — returns structured JSON errors instead of HTML.</summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        (int statusCode, string message) = ex switch
        {
            UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "Unauthorized"),
            InvalidOperationException => ((int)HttpStatusCode.BadRequest, ex.Message),
            ArgumentException => ((int)HttpStatusCode.BadRequest, ex.Message),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;
        var response = ApiResponse<object>.Fail(message);

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}

/// <summary>Restricts Hangfire dashboard to authenticated SuperAdmins.</summary>
public class HangfireAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("SuperAdmin");
    }
}
