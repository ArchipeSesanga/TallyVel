using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Common;

public sealed class TallyVelExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<TallyVelExceptionHandler> _logger;

    public TallyVelExceptionHandler(
        IProblemDetailsService problemDetailsService, ILogger<TallyVelExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Not one of ours → it's a bug. Let the default handler return a
        // plain 500 so no internal details reach the client.
        if (exception is not TallyVelException tallyVelException)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        var (statusCode, title) = MapToResponse(tallyVelException);

        // Expected domain failures: a warning, not an error, and no stack trace.
        _logger.LogWarning("{Method} {Path} rejected with {StatusCode} ({Code}): {Message}",
            httpContext.Request.Method, httpContext.Request.Path, statusCode,
            tallyVelException.Code, tallyVelException.Message);
        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = tallyVelException.Message,
            // Tag URI (RFC 4151): a stable identifier we own, without
            // pretending to be a web page on a domain we don't control.
            Type = $"tag:tallyvel,2026:problem/{tallyVelException.Code}",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = tallyVelException.Code;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = tallyVelException,
            ProblemDetails = problem,
        });
    }

    // The ONLY place a failure type is turned into a status code.
    //   400 — a list query's parameters are invalid (negative pageSize,
    //         unknown sort, bad or mismatched page token)
    //   404 — the request points at something that doesn't exist
    //   422 — the request can't be processed as sent: a domain rule forbids
    //         it, or an Idempotency-Key was reused with a different body
    //   409 — it clashes with current state: the thing already exists, or
    //         a request with the same Idempotency-Key is still running
    // Two kinds share 422 and two share 409 on purpose; clients tell them
    // apart by Type / code, not by status.
    private static (int StatusCode, string Title) MapToResponse(TallyVelException exception) =>
        exception switch
        {
            PageSizeException                 => (StatusCodes.Status400BadRequest,          "Invalid Page Size"),
            InvalidQueryException             => (StatusCodes.Status400BadRequest,          "Invalid Query"),
            NotFoundException                 => (StatusCodes.Status404NotFound,            "Not Found"),
            BusinessRuleViolationException    => (StatusCodes.Status422UnprocessableEntity, "Business Rule Violation"),
            AlreadyExistsException            => (StatusCodes.Status409Conflict,            "Already Exists"),
            IdempotencyKeyReusedException     => (StatusCodes.Status422UnprocessableEntity, "Idempotency Key Reused"),
            IdempotencyKeyInProgressException => (StatusCodes.Status409Conflict,            "Request Still In Progress"),

            // Fail loudly if a new TallyVelException is added without a
            // mapping, instead of silently becoming a 500.
            _ => throw new InvalidOperationException(
                $"{exception.GetType().Name} has no mapping in {nameof(TallyVelExceptionHandler)}."),
        };
}