using CardGames.Poker.Api.Clients;
using CardGames.Poker.Web.Components;
using CardGames.Poker.Web.Components.Account;
using CardGames.Poker.Web.Data;
using CardGames.Poker.Web.Infrastructure;
using CardGames.Poker.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Refit;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

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

// Register Dashboard UI state service (scoped per Blazor circuit)
builder.Services.AddScoped<DashboardState>();

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

		// Pull basic profile fields when available (e.g. given_name, family_name, picture).
		options.Scope.Add("profile");
		options.ClaimActions.Add(new JsonKeyClaimAction("picture", ClaimValueTypes.String, "picture"));
	})
	.AddOAuth("Yahoo", "Yahoo", options =>
	{
		options.ClientId = builder.Configuration["Authentication:Yahoo:ClientId"] ?? throw new InvalidOperationException("Yahoo ClientId not configured");
		options.ClientSecret = builder.Configuration["Authentication:Yahoo:ClientSecret"] ?? throw new InvalidOperationException("Yahoo ClientSecret not configured");

		options.CallbackPath = "/signin-oidc";

		// Yahoo OAuth 2.0 endpoints
		options.AuthorizationEndpoint = "https://api.login.yahoo.com/oauth2/request_auth";
		options.TokenEndpoint = "https://api.login.yahoo.com/oauth2/get_token";
		options.UserInformationEndpoint = "https://api.login.yahoo.com/openid/v1/userinfo";

		// Yahoo uses 'openid' scope. To get profile/email data, you must enable
		// "Profile" and "Email" under "OpenID Connect Permissions" in your
		// Yahoo Developer app settings at https://developer.yahoo.com/apps/
		options.Scope.Clear();
		options.Scope.Add("openid");

		options.SaveTokens = true;

		// Map claims from the userinfo response
		options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
		options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
		options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
		options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
		options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
		options.ClaimActions.MapJsonKey("picture", "picture");

		options.Events.OnCreatingTicket = async context =>
		{
			// Fetch user info from Yahoo's userinfo endpoint
			var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
			request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

			var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
			response.EnsureSuccessStatusCode();

			var user = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
			context.RunClaimActions(user);

			// If 'name' claim is missing, try to construct it from given_name + family_name
			if (context.Principal?.FindFirst(ClaimTypes.Name) is null)
			{
				var givenName = user.TryGetProperty("given_name", out var gn) ? gn.GetString() : null;
				var familyName = user.TryGetProperty("family_name", out var fn) ? fn.GetString() : null;
				var fullName = string.Join(" ", new[] { givenName, familyName }.Where(s => !string.IsNullOrEmpty(s)));

				if (!string.IsNullOrEmpty(fullName) && context.Principal?.Identity is ClaimsIdentity identity)
				{
					identity.AddClaim(new Claim(ClaimTypes.Name, fullName));
				}
			}
		};
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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;

    // Dev tunnels / local proxies typically won't be in KnownNetworks/KnownProxies.
    // Clearing these allows forwarded headers to flow in development.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseForwardedHeaders();

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
