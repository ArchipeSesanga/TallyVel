using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallyVel.Api.Data;

namespace TallyVel.Tests.Integration;

/// <summary>
/// A real PostgreSQL database for tests that must prove what the database
/// itself enforces. The EF InMemory provider ignores unique indexes and
/// SQLite behaves differently from Postgres, so neither can stand in.
///
/// Setup: create a database whose name ends in "_test" (owned by the app
/// user) and put its connection string in the TALLYVEL_TEST_CONNECTION
/// environment variable. Migrations are applied once when the fixture
/// starts. Tests use fresh Guid-based data, so nothing needs cleaning up.
///
/// If the variable is missing the tests FAIL with instructions, rather than
/// skipping and quietly hiding that nothing was checked.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "TALLYVEL_TEST_CONNECTION";

    private DbContextOptions<TallyVelDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"These tests need a real PostgreSQL test database. Create one whose name ends in '_test' " +
                $"(e.g. tallyvel_test) and set {ConnectionVariable} to its connection string.");
        }

        // Safety: migrations are applied automatically, so never point this
        // at the dev or perf database.
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
        if (!database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run against '{database}': the test database name must end in '_test'.");
        }

        _options = new DbContextOptionsBuilder<TallyVelDbContext>().UseNpgsql(connectionString).Options;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>A new context each call, so tests can use separate units of work.</summary>
    public TallyVelDbContext CreateContext() => new(_options);

    public Task DisposeAsync() => Task.CompletedTask;
}
