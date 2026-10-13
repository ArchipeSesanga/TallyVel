using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Api.Application.Interfaces;
using Api.Application.Services;
using Api.Common;
using Api.Filters;
using Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

builder.Services.AddDbContext<TallyVelDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("TallyVel"),
        npgsql => npgsql.MigrationsAssembly(typeof(TallyVelDbContext).Assembly.FullName)));

// Repositories are Scoped: they share the request's DbContext.
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionRepository, EfContributionRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
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

//This register the password hasher
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();



//JWT decode
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey is missing or shorter than 32 bytes. " +
        "Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"<random 32+ byte value>\"")
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

builder.Services.AddAuthorization();


//TimeProvider emulate time in tests later, 
// for example to test an expired refresh token without waiting. 
// JsonWebTokenHandler comes with the JwtBearer package.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenService, TokenService>();


var app = builder.Build();
if (args.Contains("--seed-volume"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallyVelDbContext>();
    await VolumeSeeder.RunAsync(db);
    return; // seed 
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
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }