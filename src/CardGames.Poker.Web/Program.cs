using CardGames.Poker.Api.Clients;
using CardGames.Poker.Web.Components;
using CardGames.Poker.Web.Components.Account;
using CardGames.Poker.Web.Data;
using CardGames.Poker.Web.Infrastructure;
using CardGames.Poker.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddServiceDiscovery();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Register services for forwarding user identity to backend API
builder.Services.AddScoped<CircuitServicesAccessor>();
builder.Services.AddScoped<CircuitHandler, CircuitServicesActivityHandler>();
builder.Services.AddTransient<AuthenticationStateHandler>();

// Register SignalR game hub client (scoped per Blazor circuit)
builder.Services.AddScoped<GameHubClient>();

// Register SignalR lobby hub client (scoped per Blazor circuit)
builder.Services.AddScoped<LobbyHubClient>();

builder.Services.ConfigureHttpClientDefaults(http =>
{
	http.AddServiceDiscovery();
});

builder.Services
	.AddRefitClient<IFiveCardDrawApi>(
		settingsAction: _ => new RefitSettings(),
		httpClientName: "fiveCardDrawApi")
	.ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://api"))
	.AddHttpMessageHandler<AuthenticationStateHandler>();

builder.Services
	.AddRefitClient<IAvailablePokerGamesApi>(
		settingsAction: _ => new RefitSettings(),
		httpClientName: "availablePokerGamesApi")
	.ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://api"))
	.AddHttpMessageHandler<AuthenticationStateHandler>();

builder.Services
	.AddRefitClient<IActiveGamesApi>(
		settingsAction: _ => new RefitSettings(),
		httpClientName: "activeGamesApi")
	.ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://api"))
	.AddHttpMessageHandler<AuthenticationStateHandler>();


builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not configured");
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("cardsdb")
	?? builder.Configuration.GetConnectionString("DefaultConnection")
	?? throw new InvalidOperationException("Connection string 'cardsdb' (Aspire) or 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();



app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.Run();
