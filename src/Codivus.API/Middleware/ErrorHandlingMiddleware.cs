using System.Net;
using System.Text.Json;
using Codivus.Core.Models;

namespace Codivus.API.Middleware;

/// <summary>
/// Middleware for handling exceptions globally
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment env)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception occurred");

        var statusCode = HttpStatusCode.InternalServerError;
        var errorResponse = new ApiErrorResponse
        {
            ErrorMessage = "An unexpected error occurred",
            StatusCode = (int)statusCode
        };

        // Add stack trace in development environment
        if (_env.IsDevelopment())
        {
            errorResponse.StackTrace = exception.StackTrace;
            errorResponse.ExceptionType = exception.GetType().Name;
            errorResponse.ErrorMessage = exception.Message;
        }

        var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(result);
    }
}

/// <summary>
/// Extension method to add the error handling middleware to the HTTP request pipeline
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}

/// <summary>
/// API error response model
/// </summary>
public class ApiErrorResponse
{
    public string ErrorMessage { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? StackTrace { get; set; }
    public string? ExceptionType { get; set; }
}
