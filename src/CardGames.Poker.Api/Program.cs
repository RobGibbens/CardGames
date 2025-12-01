using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

using CardGames.Poker.Api.Features.Auth;
using CardGames.Poker.Api.Features.Chat;
using CardGames.Poker.Api.Features.Friends;
using CardGames.Poker.Api.Features.Hands;
using CardGames.Poker.Api.Features.History;
using CardGames.Poker.Api.Features.Showdown;
using CardGames.Poker.Api.Features.Simulations;
using CardGames.Poker.Api.Features.Tables;
using CardGames.Poker.Api.Features.Variants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add authentication services
builder.Services.AddAuthServices(builder.Configuration);

// Add friends repository
builder.Services.AddSingleton<IFriendsRepository, InMemoryFriendsRepository>();

// Add history repository
builder.Services.AddSingleton<IHistoryRepository, InMemoryHistoryRepository>();

// Add tables repository
builder.Services.AddSingleton<ITablesRepository, InMemoryTablesRepository>();

// Add game variant factory and built-in variants
builder.Services.AddGameVariantFactory();
builder.Services.AddBuiltInVariants();

// Add showdown coordinator services
builder.Services.AddShowdownServices();

// Add chat services
builder.Services.AddSingleton<IChatMessageValidator, ChatMessageValidator>();
builder.Services.AddSingleton<IChatService, ChatService>();

// Add SignalR services
builder.Services.AddSignalR();

// Add connection mapping service for player-to-connection tracking
builder.Services.AddSingleton<IConnectionMappingService, ConnectionMappingService>();

// Add connection health monitoring background service
builder.Services.AddHostedService<ConnectionHealthMonitorService>();

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
app.MapChatEndpoints();
app.MapFriendsEndpoints();
app.MapHandsEndpoints();
app.MapHistoryEndpoints();
app.MapSimulationsEndpoints();
app.MapTablesEndpoints();
app.MapVariantsEndpoints();


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
