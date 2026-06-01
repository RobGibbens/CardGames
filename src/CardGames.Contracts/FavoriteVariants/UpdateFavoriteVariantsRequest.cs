namespace CardGames.Poker.Api.Contracts;

public sealed record UpdateFavoriteVariantsRequest
{
	public required IReadOnlyList<string> FavoriteVariantCodes { get; init; }
}