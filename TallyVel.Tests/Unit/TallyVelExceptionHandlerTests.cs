using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TallyVel.Api.Common;
using TallyVel.Api.Domain;

namespace TallyVel.Tests.Unit;

public class TallyVelExceptionHandlerTests
{
    // What Postgres really says, including the constraint name and row data.
    // None of this may ever reach a client.
    private const string RawPostgresMessage =
        "duplicate key value violates unique constraint \"IX_Contributions_StokvelId_MemberUserId_Cycle\"";

    private readonly RecordingLogger _logger = new();

    private (TallyVelExceptionHandler Handler, DefaultHttpContext Context, MemoryStream Body) Arrange()
    {
        // The real problem-details service, so the body is written exactly as
        // it is in the running API.
        var services = new ServiceCollection().AddOptions().AddProblemDetails().BuildServiceProvider();

        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Path = "/api/stokvels/abc/contributions";
        context.Response.Body = body;

        var handler = new TallyVelExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(), _logger);

        return (handler, context, body);
    }

    private static DbUpdateException DbUpdate(string sqlState, string? constraint = null) =>
        new("An error occurred while saving the entity changes.",
            new PostgresException(
                messageText: RawPostgresMessage,
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: sqlState,
                detail: "Key (\"StokvelId\", \"MemberUserId\", \"Cycle\")=(secret-row-data) already exists.",
                constraintName: constraint));

    private static JsonElement ReadJson(MemoryStream body)
    {
        body.Position = 0;
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string ReadText(MemoryStream body)
    {
        body.Position = 0;
        return new StreamReader(body).ReadToEnd();
    }

    [Fact]
    public async Task UniqueViolation_23505_IsMappedTo409_AsProblemJson()
    {
        var (handler, context, body) = Arrange();

        var handled = await handler.TryHandleAsync(
            context, DbUpdate("23505", "IX_Contributions_StokvelId_MemberUserId_Cycle"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        // Same shape as the other handled failures: status, title, detail,
        // type (tag URI), instance and the "code" extension.
        var json = ReadJson(body);
        Assert.Equal(409, json.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", json.GetProperty("title").GetString());
        Assert.Equal("The request conflicts with an existing record.", json.GetProperty("detail").GetString());
        Assert.Equal("tag:tallyvel,2026:problem/unique-violation", json.GetProperty("type").GetString());
        Assert.Equal("/api/stokvels/abc/contributions", json.GetProperty("instance").GetString());
        Assert.Equal("unique-violation", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UniqueViolation_23505_DoesNotLeakPostgresDetailsToTheClient()
    {
        var (handler, context, body) = Arrange();

        await handler.TryHandleAsync(
            context, DbUpdate("23505", "IX_Contributions_StokvelId_MemberUserId_Cycle"), CancellationToken.None);

        Assert.NotEqual(0, body.Length); // a body was written, so the checks below mean something
        var text = ReadText(body);
        Assert.DoesNotContain("duplicate key", text);
        Assert.DoesNotContain("IX_Contributions", text);
        Assert.DoesNotContain("secret-row-data", text);
        Assert.DoesNotContain("StokvelId", text);
    }

    [Fact]
    public async Task UniqueViolation_23505_LogsTheConstraintNameAtWarning()
    {
        var (handler, context, _) = Arrange();

        await handler.TryHandleAsync(
            context, DbUpdate("23505", "IX_Contributions_StokvelId_MemberUserId_Cycle"), CancellationToken.None);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("IX_Contributions_StokvelId_MemberUserId_Cycle", entry.Message);
    }

    [Fact]
    public async Task ForeignKeyViolation_23503_IsNotTurnedInto409()
    {
        var (handler, context, body) = Arrange();

        var handled = await handler.TryHandleAsync(
            context, DbUpdate("23503", "FK_Contributions_Users_MemberUserId"), CancellationToken.None);

        // Not handled: the default handler turns it into a plain 500, as before.
        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task DbUpdateException_WithoutAPostgresInnerException_IsNotTurnedInto409()
    {
        var (handler, context, body) = Arrange();

        var handled = await handler.TryHandleAsync(
            context, new DbUpdateException("save failed", new InvalidOperationException("boom")),
            CancellationToken.None);

        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task AlreadyExistsException_StillMapsTo409_WithItsOwnCode()
    {
        // The repository's own translation must keep working unchanged.
        var (handler, context, body) = Arrange();

        var handled = await handler.TryHandleAsync(
            context,
            new AlreadyExistsException("contribution-already-recorded", "User x has already contributed."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var json = ReadJson(body);
        Assert.Equal("Already Exists", json.GetProperty("title").GetString());
        Assert.Equal("contribution-already-recorded", json.GetProperty("code").GetString());
        Assert.Equal("tag:tallyvel,2026:problem/contribution-already-recorded", json.GetProperty("type").GetString());
    }

    // A tiny logger that remembers what it was asked to write.
    private sealed class RecordingLogger : ILogger<TallyVelExceptionHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
