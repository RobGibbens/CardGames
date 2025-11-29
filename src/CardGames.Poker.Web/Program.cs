using CardGames.Poker.Web.Components;
using CardGames.Poker.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register authentication services
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<IAuthStateManager>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FriendsService>();
builder.Services.AddAuthorizationCore();

// Register HTTP client for API calls
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";
builder.Services.AddHttpClient("PokerApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Register SignalR client service
builder.Services.AddScoped<GameHubService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
