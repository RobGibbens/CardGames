namespace CardGames.Poker.Api.Data.Entities;

public sealed class UserGamePreferences : EntityWithRowVersion
{
	public string UserId { get; set; } = null!;

	public int DefaultSmallBlind { get; set; }

	public int DefaultBigBlind { get; set; }

	public int DefaultAnte { get; set; }

	public int DefaultMinimumBet { get; set; }

	public string? FavoriteVariantCodesJson { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public ApplicationUser User { get; set; } = null!;
}
