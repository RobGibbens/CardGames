namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class CardDeckStorageOptions
{
	public const string SectionName = "CardDeckStorage";

	public string ContainerName { get; set; } = "card-decks";
}
