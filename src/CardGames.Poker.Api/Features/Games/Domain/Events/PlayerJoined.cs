namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a player joins a poker game.
/// </summary>
public record PlayerJoined(
	Guid GameId,
	Guid PlayerId,
	string PlayerName,
	int BuyIn,
	int Position,
	DateTime JoinedAt
);