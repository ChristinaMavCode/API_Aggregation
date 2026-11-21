using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API_Aggregation.Application.Middleware;

public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
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
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Validation failed: " + ex.Errors.Select(e => e.ErrorMessage));

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            var errorMessages = string.Join(", ", ex.Errors.Select(e => e.ErrorMessage));
            await context.Response.WriteAsJsonAsync(new { detail = "Validation failed: " + errorMessages });
        }
    }
}