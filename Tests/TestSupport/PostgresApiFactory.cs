using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

using Infrastructure.Data;
using Api.Jobs;
using Api.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.TestSupport;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public CapturedLogs Logs { get; } = new();

    /// <summary>The password of every seeded demo user. TEST-ONLY, random on every run, never committed.</summary>
    public string DemoPassword { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("tallyvel_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // DatabaseSeeder (run by Program.cs in Development) throws if any
        // migration is pending. Apply them to the brand-new container BEFORE
        // anything boots the app (CreateClient / Services do that lazily).
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, NOT ConfigureAppConfiguration: Program.cs reads the
        // connection string immediately, and ConfigureAppConfiguration values
        // arrive too late. Exact key Program.cs reads: "TallyVel".
        builder.UseSetting("ConnectionStrings:TallyVel", _container.GetConnectionString());

        builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(Logs));

        // Every test client shares one "IP", and many tests log in. Lift the
        // login limit here; the rate-limit tests set their own small limits.
        builder.UseSetting("RateLimiting:Login:PermitLimit", "10000");

        // Explicit, so the CORS tests don't depend on appsettings.Development.json.
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");

        // The hosted cycle-close job must never run on its own during tests;
        // tests drive a CycleCloser directly with a fake clock.
        builder.UseSetting("Jobs:CycleClose:Enabled", "false");

        // Self-contained: don't depend on the developer's user-secrets. TEST-ONLY
        // key, generated fresh on every run so no key value is ever committed.
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

        // The Development seeder hashes this as every demo user's password.
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
    }

    /// <summary>A cycle closer on the test database with the given clock.</summary>
    public CycleCloser CreateCloser(TimeProvider clock, int maxCyclesPerRun = 1000) =>
        new(Services.GetRequiredService<IServiceScopeFactory>(), clock,
            Options.Create(new CycleCloseOptions { MaxCyclesPerRun = maxCyclesPerRun }),
            Services.GetRequiredService<TallyVelMetrics>(), NullLogger<CycleCloser>.Instance);

    /// <summary>
    /// A fresh DbContext on the container, for tests that talk to the database
    /// directly. Same name and shape as PostgresFixture.CreateContext(), so
    /// DatabaseConstraintTests' test bodies don't change.
    /// </summary>
    public TallyVelDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TallyVelDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Postgres collection")]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { }