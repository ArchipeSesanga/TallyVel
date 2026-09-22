using Scalar.AspNetCore;
using TallyVel.Api.Data;
using TallyVel.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var (seedUsers, seedStokvels) = SeedData.Generate();

builder.Services.AddOpenApi();

builder.Services.AddSingleton<IUserRepository>(new InMemoryUserRepository(seedUsers));
builder.Services.AddSingleton<IStokvelRepository>(new InMemoryStokvelRepository(seedStokvels));
builder.Services.AddSingleton<IContributionRepository, InMemoryContributionRepository>();
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddControllers();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
