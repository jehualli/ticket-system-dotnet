using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Jehualli.TicketSystem.Api.Errors;

public record ErrorResponse(
    DateTime Timestamp,
    int Status,
    string Error,
    string Message,
    string Path,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IDictionary<string, string[]>? FieldErrors = null);

// Turns exceptions thrown by services into a consistent JSON error body.
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, error) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            BadRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning("{Status} on {Method} {Path}: {Message}",
                status, context.Request.Method, context.Request.Path, exception.Message);
        }

        var message = status == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred. Please try again later."
            : exception.Message;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse(DateTime.UtcNow, status, error, message, context.Request.Path), ct);
        return true;
    }

    // Used for [ApiController] model validation failures, so they share the same shape.
    public static IActionResult ValidationResponse(ActionContext context)
    {
        var fieldErrors = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kv => JsonNamingPolicy.CamelCase.ConvertName(kv.Key),
                kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var body = new ErrorResponse(
            DateTime.UtcNow,
            StatusCodes.Status400BadRequest,
            "Validation Failed",
            "Request validation failed. Check fieldErrors for details.",
            context.HttpContext.Request.Path,
            fieldErrors);
        return new BadRequestObjectResult(body);
    }
}
