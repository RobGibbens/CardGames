namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Represents a player in the game aggregate.
/// </summary>
public record GamePlayer(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int Position
);