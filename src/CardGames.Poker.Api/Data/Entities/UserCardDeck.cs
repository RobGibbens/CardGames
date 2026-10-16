namespace CardGames.Poker.Api.Data.Entities;

public sealed class UserCardDeck : EntityWithRowVersion
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = null!;

	public string Name { get; set; } = null!;

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public ApplicationUser User { get; set; } = null!;

	public ICollection<UserCardDeckImage> Images { get; set; } = new List<UserCardDeckImage>();
}
