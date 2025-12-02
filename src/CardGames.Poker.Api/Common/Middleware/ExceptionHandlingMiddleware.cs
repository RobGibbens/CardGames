using FluentValidation;
using System.Text.Json;

namespace CardGames.Poker.Api.Common.Middleware;

/// <summary>
/// Middleware that handles exceptions and converts them to appropriate HTTP responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/plain";

        // Get the first error message for consistency with existing behavior
        var errorMessage = exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed";

        await context.Response.WriteAsync(errorMessage);
    }
}

/// <summary>
/// Extension methods for adding exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
