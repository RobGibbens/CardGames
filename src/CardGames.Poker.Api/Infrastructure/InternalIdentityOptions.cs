namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     Shared secret used by the Blazor host to mint <c>X-Internal-User-Token</c> values
///     and by the API to validate them.
/// </summary>
/// <remarks>
///     <para><c>InternalIdentity:SigningKey</c> — UTF-8 secret, 32+ characters. Set the same value on Web and API
///     (Aspire parameter <c>internalIdentitySigningKey</c>, user secrets, or Key Vault). Leave empty to fail closed.</para>
///     <para><c>InternalIdentity:AllowUnsafeHeaders</c> — when true, unsigned <c>X-User-*</c> headers and query
///     identity are accepted. Development only. Default false. Never enable in Production.</para>
///     <para><c>InternalIdentity:TokenLifetimeMinutes</c> — lifetime of tokens minted by Web. Default 5. Maximum 15.</para>
///     <para><c>Cors:AllowedOrigins</c> — browser origins allowed when the host is not Development.</para>
/// </remarks>
public sealed class InternalIdentityOptions
{
	public const string SectionName = "InternalIdentity";

	public string SigningKey { get; set; } = "";

	public bool AllowUnsafeHeaders { get; set; }

	public int TokenLifetimeMinutes { get; set; } = 5;
}
