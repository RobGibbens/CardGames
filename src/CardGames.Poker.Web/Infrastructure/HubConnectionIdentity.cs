using System.Security.Claims;
using CardGames.Poker.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Configuration;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
///     Mints the HMAC identity token the API accepts, and attaches it to SignalR connections.
/// </summary>
public static class HubConnectionIdentity
{
	public const string SigningKeyName = "InternalIdentity:SigningKey";

	public static void Apply(HttpConnectionOptions options, IConfiguration configuration,
		AuthenticationStateProvider authStateProvider)
	{
		options.AccessTokenProvider = async () =>
		{
			var state = await authStateProvider.GetAuthenticationStateAsync();
			return Issue(configuration, state.User);
		};
	}

	public static string? Issue(IConfiguration configuration, ClaimsPrincipal user)
	{
		if (user.Identity?.IsAuthenticated != true)
			return null;

		var signingKey = configuration[SigningKeyName];
		if (string.IsNullOrWhiteSpace(signingKey))
			return null;

		var minutes = configuration.GetValue("InternalIdentity:TokenLifetimeMinutes", 5);
		if (minutes < 1) minutes = 1;
		if (minutes > 15) minutes = 15;

		var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
		if (string.IsNullOrWhiteSpace(userId))
			return null;

		var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
		var name = user.FindFirstValue(ClaimTypes.Name)
		           ?? user.FindFirstValue("name")
		           ?? email
		           ?? user.Identity.Name;

		return InternalUserToken.Issue(signingKey, userId, email, name, DateTimeOffset.UtcNow.AddMinutes(minutes));
	}

	public static string HubUrl(IConfiguration configuration, string hubPath)
	{
		var baseUrl = configuration["Services:Api:Https:0"]
		              ?? configuration["Services:Api:Http:0"]
		              ?? "https://localhost:7001";
		return $"{baseUrl.TrimEnd('/')}{hubPath}";
	}

	public static bool AllowUnsafeHeaders(IConfiguration configuration) =>
		configuration.GetValue("InternalIdentity:AllowUnsafeHeaders", false);
}
