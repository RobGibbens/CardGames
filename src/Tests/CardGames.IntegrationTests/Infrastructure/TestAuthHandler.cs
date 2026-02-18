using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CardGames.IntegrationTests.Infrastructure;

public sealed class TestAuthHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "TestAuth";
	public const string UserHeader = "X-Test-UserId";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var headerValue = Request.Headers.TryGetValue(UserHeader, out var values) ? values.FirstOrDefault() : null;
		var userId = !string.IsNullOrWhiteSpace(headerValue)
			? headerValue.Trim()
			: "test-user";

		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, userId),
			new Claim(ClaimTypes.Name, userId)
		};

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}