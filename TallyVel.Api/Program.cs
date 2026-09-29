using FluentValidation;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Application.Services;
using TallyVel.Api.Common;
using TallyVel.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var (seedUsers, seedStokvels) = SeedData.Generate();

builder.Services.AddOpenApi();

builder.Services.AddSingleton<IUserRepository>(new InMemoryUserRepository(seedUsers));
builder.Services.AddSingleton<IStokvelRepository>(new InMemoryStokvelRepository(seedStokvels));
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<IContributionRepository, InMemoryContributionRepository>();
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<IContributionService, ContributionServices>();
builder.Services.AddControllers();

builder.Services.AddExceptionHandler<TallyVelExceptionHandler>();
builder.Services.AddProblemDetails();

// This automatically scans and registers all validators found in the same assembly as Program
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

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