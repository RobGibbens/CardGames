using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

using CardGames.Poker.Api.Features.Auth;
using CardGames.Poker.Api.Features.Friends;
using CardGames.Poker.Api.Features.Hands;
using CardGames.Poker.Api.Features.Simulations;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add authentication services
builder.Services.AddAuthServices(builder.Configuration);

// Add friends repository
builder.Services.AddSingleton<IFriendsRepository, InMemoryFriendsRepository>();

// Add SignalR services
builder.Services.AddSignalR();

// Add CORS for SignalR (configure properly for production)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow any origin in development
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetIsOriginAllowed(_ => true)
                  .AllowCredentials();
        }
        else
        {
            // In production, restrict to known origins
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithOrigins(allowedOrigins)
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable CORS
app.UseCors();

app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map feature endpoints
app.MapAuthEndpoints();
app.MapFriendsEndpoints();
app.MapHandsEndpoints();
app.MapSimulationsEndpoints();


// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

// Demo endpoint to trigger a broadcast message
app.MapPost("/api/broadcast", async (string message, IHubContext<GameHub> hubContext) =>
{
    await hubContext.Clients.All.SendAsync("ReceiveMessage", message, DateTime.UtcNow);
    return Results.Ok(new { Message = "Broadcast sent", Content = message, Timestamp = DateTime.UtcNow });
})
.WithName("BroadcastMessage");

app.Run();

// Expose the Program class for integration testing
public partial class Program { }
