using KeyloopScheduler.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeyloopScheduler.Api.Filters;

/// <summary>
/// Translates domain and persistence exceptions into RFC 7807 problem responses,
/// so HTTP status semantics live in exactly one place.
/// </summary>
public sealed class GlobalExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var exception = context.Exception;
        var (statusCode, title) = Map(exception);
        var traceId = context.HttpContext.TraceIdentifier;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request rejected. Status={StatusCode} TraceId={TraceId}",
                statusCode,
                traceId);
        }

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred while processing the request."
                : exception.Message,
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };

        context.ExceptionHandled = true;

        return Task.CompletedTask;
    }

    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        DomainValidationException => (StatusCodes.Status400BadRequest, "Invalid appointment request"),
        EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
        ScheduleConflictException => (StatusCodes.Status409Conflict, "Scheduling conflict"),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Scheduling conflict"),
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } =>
            (StatusCodes.Status409Conflict, "Scheduling conflict"),
        PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected } =>
            (StatusCodes.Status409Conflict, "Scheduling conflict"),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request"),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error")
    };
}
