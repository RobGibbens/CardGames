namespace CardGames.Poker.Api.Data.Entities;

public sealed class UserCardDeckImage
{
	public Guid Id { get; set; }

	public Guid DeckId { get; set; }

	public string SlotKey { get; set; } = null!;

	public string? Suit { get; set; }

	public string? Rank { get; set; }

	public bool IsBack { get; set; }

	public string BlobName { get; set; } = null!;

	public string ContentType { get; set; } = null!;

	public int Width { get; set; }

	public int Height { get; set; }

	public UserCardDeck Deck { get; set; } = null!;
}
