using CardGames.Poker.Api.Data.Entities;
using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

/// <summary>
/// Response containing all properties of a specific game, including the game type code.
/// This is used by the generic Games API which returns games regardless of type.
/// </summary>
public sealed record GetGameResponse
{
	public required Guid Id { get; init; }
	public required Guid GameTypeId { get; init; }
	public required string? GameTypeCode { get; init; }
	public required string? GameTypeName { get; init; }
	public string? Name { get; init; }
	public required int MinimumNumberOfPlayers { get; init; }
	public required int MaximumNumberOfPlayers { get; init; }
	public required string CurrentPhase { get; init; }
	public string? CurrentPhaseDescription { get; init; }
	public required int CurrentHandNumber { get; init; }
	public required int DealerPosition { get; init; }
	public int? Ante { get; init; }
	public int? SmallBlind { get; init; }
	public int? BigBlind { get; init; }
	public int? BringIn { get; init; }
	public int? SmallBet { get; init; }
	public int? BigBet { get; init; }
	public int? MinBet { get; init; }
	public string? GameSettings { get; init; }
	public required GameStatus Status { get; init; }
	public required int CurrentPlayerIndex { get; init; }
	public required int BringInPlayerIndex { get; init; }
	public required int CurrentDrawPlayerIndex { get; init; }
	public int? RandomSeed { get; init; }
	public required DateTimeOffset CreatedAt { get; init; }
	public required DateTimeOffset UpdatedAt { get; init; }
	public DateTimeOffset? StartedAt { get; init; }
	public DateTimeOffset? EndedAt { get; init; }
	public string? CreatedById { get; init; }
	public string? CreatedByName { get; init; }
	public required bool CanContinue { get; init; }
	public required string RowVersion { get; init; }
}
