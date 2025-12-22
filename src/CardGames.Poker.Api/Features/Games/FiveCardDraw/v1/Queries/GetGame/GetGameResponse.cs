using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

/// <summary>
/// Response containing all properties of a specific game.
/// </summary>
public record GetGameResponse(
Guid Id,
Guid GameTypeId,
string? Name,
int MinimumNumberOfPlayers,
int MaximumNumberOfPlayers,
string CurrentPhase,
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
