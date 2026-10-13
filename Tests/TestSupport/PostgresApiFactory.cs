using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

using Infrastructure.Data;

namespace Tests.TestSupport;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
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

        // Self-contained: don't depend on the developer's user-secrets. TEST-ONLY
        // key, generated fresh on every run so no key value is ever committed.
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    }

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