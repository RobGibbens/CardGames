using CardGames.Poker.Api.Features.Games.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a new poker game is created.
/// </summary>
public record GameCreated(
	Guid GameId,
	GameType GameType,
	GameConfiguration Configuration,
	DateTime CreatedAt
);