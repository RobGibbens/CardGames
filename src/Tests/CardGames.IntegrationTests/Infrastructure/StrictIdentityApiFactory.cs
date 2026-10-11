namespace CardGames.IntegrationTests.Infrastructure;

public static class TrustedIdentityTestKey
{
	public const string Value = "dev-only-internal-identity-signing-key-32b";
}

public sealed class StrictIdentityApiFactory : ApiWebApplicationFactory
{
	public StrictIdentityApiFactory()
		: base(false)
	{
	}
}
