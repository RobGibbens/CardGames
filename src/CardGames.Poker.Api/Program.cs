using CardGames.Poker.Api.Features.Hands;
using CardGames.Poker.Api.Features.Simulations;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map feature endpoints
app.MapHandsEndpoints();
app.MapSimulationsEndpoints();

app.Run();

// Expose the Program class for integration testing
public partial class Program { }
