using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Api.Application.Interfaces;
using Api.Authorization;
using Api.Application.Services;
using Api.Common;
using Api.Validators;
using Api.Filters;
using Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using Api.Jobs;
using Api.Observability;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Fail at startup, not on the first request, when the database isn't configured.
var connectionString = builder.Configuration.GetConnectionString("TallyVel");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Missing setting ConnectionStrings:TallyVel " +
        "(environment variable ConnectionStrings__TallyVel).");

// Tagged "ready": only readiness runs it. Liveness must never depend on the
// database, or one database blip restarts every container at once.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TallyVelDbContext>("database", tags: ["ready"]);

// A probe must answer fast: a slow answer is an unhealthy answer.
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
        registration.Timeout = TimeSpan.FromSeconds(3);
});

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
// The custom DateTimeOffset converter (writes Z) hides the type from the schema exporter,
// so say what it is: a date-time string.
builder.Services.AddOpenApi(options => options.AddOperationTransformer((operation, context, _) =>
{
    // The controller reads the header by hand (Request.Headers) so a missing key stays a 400 from the
    // service. Declaring it here only describes it, so generated clients send it; behavior is unchanged.
    if (context.Description.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
        action == nameof(Api.Presentation.Controllers.ContributionsController.RecordContribution))
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "A fresh UUID per attempt. Retrying the same request with the same key replays the first result instead of recording twice.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        });
    }
    return Task.CompletedTask;
}).AddSchemaTransformer((schema, context, _) =>
{
    var type = context.JsonTypeInfo.Type;
    if (type == typeof(DateTimeOffset))
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = "date-time";
    }
    else if (type == typeof(DateTimeOffset?))
    {
        schema.Type = JsonSchemaType.String | JsonSchemaType.Null;
        schema.Format = "date-time";
    }
    else if (type == typeof(Microsoft.AspNetCore.Mvc.ProblemDetails) ||
             type == typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails))
    {
        // Every error body carries these two (the error handler adds them), but the framework's
        // own type does not declare them. Without this the generated TypeScript type has no
        // correlationId, and a client has to widen it by hand to show members a reference.
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["correlationId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "The reference a member can quote to support. Also written to the server log.",
        };
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "The W3C trace id of the request.",
        };
    }
    return Task.CompletedTask;
}));

builder.Services.AddDbContext<TallyVelDbContext>(o =>
    o.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsAssembly(typeof(TallyVelDbContext).Assembly.FullName)));

// Repositories are Scoped: they share the request's DbContext.
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<ICycleRepository, EfCycleRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
// Still in memory — idempotency keys aren't part of the database model yet.
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionServices>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<MemberHomeService>();
// Saves changes made to tracked entities (e.g. stokvel memberships) at the
// end of each successful request.
// Enums travel as names ("Chairperson", "Monthly"). Numbers are still
// accepted on input, so older clients keep working.
// Numbers are strict: "amount": "100" is a 400, not silently accepted (the
// web default reads numbers from strings). Formatter messages are off, so
// that 400 never names an internal type such as System.Decimal.
builder.Services.AddControllers(o => o.Filters.Add<SaveChangesFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        o.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationProblems.FromModelState);
// The same settings for the OpenAPI document, so it describes numbers as
// numbers only and enums as their names.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddExceptionHandler<TallyVelExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["correlationId"] = Correlation.GetId(context.HttpContext));

// The frontend's origins come from configuration: localhost:3000 in
// Development, none at all by default. Never "*" and never the request's
// own Origin reflected back, because the refresh cookie needs credentials.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicies.Frontend, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowCredentials()
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "traceparent")
    .WithExposedHeaders("Retry-After")
    .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

builder.Services.AddTallyVelRateLimiting(builder.Configuration);

// This automatically scans and registers all validators found in the same assembly as Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

//This register the password hasher
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

//JWT decode
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey is missing or shorter than 32 bytes. " +
        "Set environment variable Jwt__SigningKey, or locally: " +
        "dotnet user-secrets set \"Jwt:SigningKey\" \"<random 32+ byte value>\"")
    .Validate(o => o.AccessTokenMinutes is > 0 and <= 15, "Jwt:AccessTokenMinutes must be 1–15.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

//this Configure JWT bearer validation
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        bearer.MapInboundClaims = false; // keep "sub" as "sub", not a long XML URI
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Issuer,
            ValidateAudience = true,
            ValidAudience = o.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Every endpoint needs a signed-in user unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // One policy per access level; the handler checks the caller's role in the stokvel from the route.
    foreach (var (name, roles) in StokvelPolicies.AllowedRoles)
        options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new StokvelRoleRequirement(roles)));
});
builder.Services.AddScoped<IAuthorizationHandler, StokvelRoleHandler>();

//TimeProvider emulate time in tests later,
// for example to test an expired refresh token without waiting.
// JsonWebTokenHandler comes with the JwtBearer package.
builder.Services.AddSingleton(TimeProvider.System);
_ = StokvelCalendar.Zone; // touch it now so a missing tzdata fails at startup, not on the first request
builder.Services.AddSingleton<ITokenService, TokenService>();

//Metrics
builder.Services.AddSingleton<TallyVelMetrics>();

// Cycle-close job
builder.Services.AddOptions<CycleCloseOptions>()
    .Bind(builder.Configuration.GetSection(CycleCloseOptions.SectionName))
    .Validate(o => o.Interval > TimeSpan.Zero && o.MaxCyclesPerRun > 0,
        "Jobs:CycleClose:Interval and MaxCyclesPerRun must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddSingleton<CycleCloser>();
builder.Services.AddHostedService<CycleCloseJob>();
var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(TallyVelTelemetry.ServiceName))
    .WithTracing(tracing => tracing
        // One span per request, except health probes (they'd bury real traffic).
        .AddAspNetCoreInstrumentation(o =>
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        // One child span per SQL command: the SQL text, never parameter values.
        .AddNpgsql()
        // Our own spans: payout.process.
        .AddSource(TallyVelTelemetry.SourceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("System.Runtime")
        .AddMeter(TallyVelTelemetry.SourceName))
    .WithLogging();

// Tests and CI never set this, so they never try to reach a dashboard.
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    openTelemetry.UseOtlpExporter();

var app = builder.Build();
if (args.Contains("--seed-volume"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallyVelDbContext>();
    await VolumeSeeder.RunAsync(db);
    return; // seed
}

// Fill an empty development database with the demo users, stokvels and contributions.
// Every demo user's password is Seed:DemoPassword (env var Seed__DemoPassword),
// hashed by the same hasher /auth/register uses. Never runs outside Development.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var demoPassword = builder.Configuration["Seed:DemoPassword"];
    await DatabaseSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<TallyVelDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder)),
        newPasswordHash: () =>
        {
            // Checked only when there is something to seed, so a machine whose database
            // is already seeded does not need the setting.
            if (string.IsNullOrWhiteSpace(demoPassword)
                || demoPassword.Length < RegisterRequestValidator.MinPasswordLength)
                throw new InvalidOperationException(
                    "Missing or too short setting Seed:DemoPassword " +
                    $"(at least {RegisterRequestValidator.MinPasswordLength} characters, like any password). " +
                    "Set environment variable Seed__DemoPassword, or locally: " +
                    "dotnet user-secrets set \"Seed:DemoPassword\" \"<password>\"");
            return hasher.HashPassword(null!, demoPassword);
        },
        clock: scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(Options =>
    {
        Options.Theme = ScalarTheme.Moon;
    }).AllowAnonymous();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready",
        new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.UseExceptionHandler();
// Turns empty 401/403/404 responses into application/problem+json.
app.UseStatusCodePages();
// After the error middleware, so 401/403/404/429/500 responses still carry
// CORS headers (otherwise the browser hides the error from the frontend).
// Before authentication, so a preflight (which has no token) is answered
// here instead of being refused by the fallback policy.
app.UseCors(CorsPolicies.Frontend);

app.UseHttpsRedirection();
app.UseAuthentication();
// After authentication, so a policy can partition by the signed-in user;
// before authorization and the controllers, so a rejected request never
// reaches ContributionServices and never stores an Idempotency-Key.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }