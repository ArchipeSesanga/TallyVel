using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallyVel.Api.Domain;
using TallyVel.Api.Infrastructure.Persistence;

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
        // Safety net: Postgres rejected a write for breaking a unique constraint
        // (SQLSTATE 23505) and no repository translated it into a domain
        // exception. That is still a conflict with an existing record, not a
        // server bug. Handled here, never in a controller. Only 23505: every
        // other DbUpdateException keeps its current behaviour.
        if (exception is DbUpdateException dbUpdateException && dbUpdateException.IsUniqueViolation())
        {
            var constraint = (dbUpdateException.InnerException as PostgresException)?.ConstraintName;

            // The constraint name is for us, not the client.
            _logger.LogWarning(
                "{Method} {Path} violated unique constraint {Constraint} and was mapped to 409",
                httpContext.Request.Method, httpContext.Request.Path, constraint);

            return await WriteProblemAsync(
                httpContext, exception, StatusCodes.Status409Conflict, "Conflict",
                "unique-violation", "The request conflicts with an existing record.");
        }

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

        return await WriteProblemAsync(
            httpContext, tallyVelException, statusCode, title, tallyVelException.Code, tallyVelException.Message);
    }

    // One place that shapes every problem response, so a mapped domain
    // failure and the unique-violation safety net look identical to clients.
    private async ValueTask<bool> WriteProblemAsync(
        HttpContext httpContext, Exception exception, int statusCode, string title, string code, string detail)
    {
        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            // Tag URI (RFC 4151): a stable identifier we own, without
            // pretending to be a web page on a domain we don't control.
            Type = $"tag:tallyvel,2026:problem/{code}",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
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
    //         (an untranslated unique violation also lands here, see above)
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