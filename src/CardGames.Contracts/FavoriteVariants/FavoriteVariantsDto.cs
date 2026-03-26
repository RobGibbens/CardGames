namespace CardGames.Poker.Api.Contracts;

public sealed record FavoriteVariantsDto
{
	public required IReadOnlyList<string> FavoriteVariantCodes { get; init; }
}