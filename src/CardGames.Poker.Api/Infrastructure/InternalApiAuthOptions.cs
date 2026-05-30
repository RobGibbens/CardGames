namespace CardGames.Poker.Api.Infrastructure;

public sealed class InternalApiAuthOptions
{
    public const string SectionName = "InternalApiAuth";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;
}