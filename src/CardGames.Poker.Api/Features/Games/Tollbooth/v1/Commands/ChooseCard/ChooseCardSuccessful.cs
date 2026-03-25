using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public record ChooseCardSuccessful : IPlayerActionResult
{
	public Guid GameId { get; init; }

	public required string PlayerName { get; init; }

	public int PlayerSeatIndex { get; init; }

	public TollboothChoice Choice { get; init; }

	public int Cost { get; init; }

	public bool OfferRoundComplete { get; init; }

	public required string CurrentPhase { get; init; }

	public int NextPlayerSeatIndex { get; init; }

	public string? NextPlayerName { get; init; }

	string? IPlayerActionResult.PlayerName => PlayerName;

	string IPlayerActionResult.ActionDescription => Choice switch
	{
		TollboothChoice.Furthest => "Took free card",
		TollboothChoice.Nearest => $"Bought nearest card ({Cost} chips)",
		TollboothChoice.Deck => $"Bought deck card ({Cost} chips)",
		_ => "Tollbooth choice"
	};
}
