using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

/// <summary>
/// Response containing all properties of a specific game, including the game type code.
/// This is used by the generic Games API which returns games regardless of type.
/// </summary>
public record GetGameResponse(
	Guid Id,
	Guid GameTypeId,
	string? GameTypeCode,
	string? GameTypeName,
	string? Name,
	int MinimumNumberOfPlayers,
	int MaximumNumberOfPlayers,
	string CurrentPhase,
	string? CurrentPhaseDescription,
	int CurrentHandNumber,
	int DealerPosition,
	int? Ante,
	int? SmallBlind,
	int? BigBlind,
	int? BringIn,
	int? SmallBet,
	int? BigBet,
	int? MinBet,
	string? GameSettings,
	GameStatus Status,
	int CurrentPlayerIndex,
	int BringInPlayerIndex,
	int CurrentDrawPlayerIndex,
	int? RandomSeed,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	DateTimeOffset? StartedAt,
	DateTimeOffset? EndedAt,
	string? CreatedById,
	string? CreatedByName,
	bool CanContinue,
	string RowVersion
);
