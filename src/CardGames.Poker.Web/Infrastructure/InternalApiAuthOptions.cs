namespace CardGames.Poker.Web.Infrastructure;

public sealed class InternalApiAuthOptions
{
    public const string SectionName = "InternalApiAuth";
    public const string InternalTokenHeaderName = "X-CardGames-Internal-Token";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 5;
}