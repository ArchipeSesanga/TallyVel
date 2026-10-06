using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Data;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Application.Services;
using TallyVel.Api.Common;
using TallyVel.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging: ILogger<T> is resolvable from DI for every class. Levels come
// from the "Logging" section of appsettings.*.json.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    o.IncludeScopes = true;
    o.SingleLine = true;
});
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("TallyVel")
    ?? throw new InvalidOperationException(
        "Connection string 'TallyVel' is not configured. Set it with " +
        "`dotnet user-secrets set \"ConnectionStrings:TallyVel\" \"...\"` " +
        "from TallyVel.Api (see README, Step 6).");

builder.Services.AddDbContext<TallyVelDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(5),
        errorCodesToAdd: null)));

// Repositories are Scoped: they share the request's DbContext.
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
// Still in memory — idempotency keys aren't part of the database model yet.
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionServices>();
// Saves changes made to tracked entities (e.g. stokvel memberships) at the
// end of each successful request.
builder.Services.AddControllers(o => o.Filters.Add<SaveChangesFilter>());

builder.Services.AddExceptionHandler<TallyVelExceptionHandler>();
builder.Services.AddProblemDetails();

// This automatically scans and registers all validators found in the same assembly as Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();
if (args.Contains("--seed-volume"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallyVelDbContext>();
    await VolumeSeeder.RunAsync(db);
    return; // seed only, never start the web host
}

// Fill an empty development database with the sample users and stokvels.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DatabaseSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<TallyVelDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder)));
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(Options =>
    {
        Options.Theme = ScalarTheme.Moon; 
    });
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }