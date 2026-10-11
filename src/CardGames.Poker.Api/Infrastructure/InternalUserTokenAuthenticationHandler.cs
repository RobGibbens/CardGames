using System.Security.Claims;
using System.Text.Encodings.Web;
using CardGames.Poker.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     Authenticates the short-lived HMAC token minted by the Blazor host.
///     Accepts <c>X-Internal-User-Token</c>, the SignalR <c>access_token</c> query value,
///     or an <c>Authorization: Bearer</c> value that is an internal token (one dot, not a JWT).
/// </summary>
public sealed class InternalUserTokenAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder,
	IOptions<InternalIdentityOptions> identityOptions)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "InternalUserToken";
	public const string HeaderName = "X-Internal-User-Token";

	public const string HubSchemes = SchemeName + "," + HeaderAuthenticationHandler.SchemeName;

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var token = ReadToken();
		if (string.IsNullOrWhiteSpace(token))
			return Task.FromResult(AuthenticateResult.NoResult());

		var signingKey = identityOptions.Value.SigningKey;
		if (!InternalUserToken.TryValidate(signingKey, token, DateTimeOffset.UtcNow, out var payload) || payload is null)
			return Task.FromResult(AuthenticateResult.Fail("Invalid internal user token."));

		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, payload.Sub),
			new("sub", payload.Sub)
		};

		if (!string.IsNullOrWhiteSpace(payload.Email))
		{
			claims.Add(new Claim(ClaimTypes.Email, payload.Email));
			claims.Add(new Claim("email", payload.Email));
		}

		if (!string.IsNullOrWhiteSpace(payload.Name))
		{
			claims.Add(new Claim(ClaimTypes.Name, payload.Name));
			claims.Add(new Claim("name", payload.Name));
		}

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
	}

	private string? ReadToken()
	{
		if (Request.Headers.TryGetValue(HeaderName, out var header) && !string.IsNullOrWhiteSpace(header))
			return header.ToString();

		if (Request.Query.TryGetValue("access_token", out var query) && !string.IsNullOrWhiteSpace(query))
			return query.ToString();

		if (!Request.Headers.TryGetValue("Authorization", out var authorization))
			return null;

		const string bearer = "Bearer ";
		var value = authorization.ToString();
		if (!value.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
			return null;

		var candidate = value[bearer.Length..].Trim();
		return candidate.Count(static c => c == '.') == 1 ? candidate : null;
	}
}
