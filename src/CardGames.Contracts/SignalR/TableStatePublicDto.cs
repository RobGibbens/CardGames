using CardGames.Poker.Api.Contracts;

namespace CardGames.Contracts.SignalR;

using System.Text.Json.Serialization;

/// <summary>
/// Public table state broadcast to all players in a game group.
/// Does not contain any private card information.
/// </summary>
public sealed record TableStatePublicDto
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The friendly name of the game.
	/// </summary>
	public string? Name { get; init; }

	/// <summary>
	/// The display name of the game variant (e.g., "Five Card Draw").
	/// </summary>
	public string? GameTypeName { get; init; }

	/// <summary>
	/// The current phase of the game (e.g., "WaitingToStart", "FirstBettingRound", "DrawPhase").
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// A friendly description of the current phase (e.g., from enum descriptions) when available.
	/// </summary>
	public string? CurrentPhaseDescription { get; init; }

	/// <summary>
	/// The ante amount required from each player.
	/// </summary>
	public int Ante { get; init; }

	/// <summary>
	/// The minimum bet amount.
	/// </summary>
	public int MinBet { get; init; }

	/// <summary>
	/// The total pot amount.
	/// </summary>
	public int TotalPot { get; init; }

	/// <summary>
	/// The seat index of the current dealer.
	/// </summary>
	public int DealerSeatIndex { get; init; }

	/// <summary>
	/// The seat index of the player who must act next.
	/// </summary>
	public int CurrentActorSeatIndex { get; init; }

	/// <summary>
	/// Whether the game is currently paused.
	/// </summary>
	public bool IsPaused { get; init; }

	/// <summary>
	/// The current hand number being played.
	/// </summary>
	public int CurrentHandNumber { get; init; }

	/// <summary>
	/// The name of the user who created the game.
	/// </summary>
	public string? CreatedByName { get; init; }

	/// <summary>
	/// The list of seats and their public state.
	/// </summary>
	public required IReadOnlyList<SeatPublicDto> Seats { get; init; }

	/// <summary>
	/// Showdown information, if the game is in showdown phase.
	/// </summary>
	public ShowdownPublicDto? Showdown { get; init; }

	/// <summary>
	/// The UTC timestamp when the current hand was completed.
	/// Used by clients to synchronize the results display period.
	/// </summary>
	public DateTimeOffset? HandCompletedAtUtc { get; init; }

	/// <summary>
	/// The UTC timestamp when the next hand is scheduled to start.
	/// Clients can use this for countdown display.
	/// </summary>
	public DateTimeOffset? NextHandStartsAtUtc { get; init; }

	/// <summary>
	/// Whether the game is currently in the results display phase
	/// (hand completed, showing results before next hand starts).
	/// </summary>
	public bool IsResultsPhase { get; init; }

	/// <summary>
	/// The number of seconds remaining until the next hand starts.
	/// Only populated during the results phase.
	/// </summary>
	public int? SecondsUntilNextHand { get; init; }

	/// <summary>
	/// Hand history entries for the dashboard flyout, sorted newest-first.
	/// Limited to the most recent entries for performance.
	/// </summary>
	public IReadOnlyList<HandHistoryEntryDto>? HandHistory { get; init; }
}

/// <summary>
/// Public state for a single seat at the table.
/// Cards are shown face-down unless they should be publicly visible.
/// </summary>
public sealed record SeatPublicDto
{
	/// <summary>
	/// The zero-based seat index.
	/// </summary>
	public int SeatIndex { get; init; }

	/// <summary>
	/// Whether the seat is occupied by a player.
	/// </summary>
	public bool IsOccupied { get; init; }

	/// <summary>
	/// The name of the player in this seat, if occupied.
	/// </summary>
	public string? PlayerName { get; init; }

	/// <summary>
	/// The player's first name, if known.
	/// </summary>
	public string? PlayerFirstName { get; init; }

	/// <summary>
	/// The player's avatar URL, if they have configured one.
	/// </summary>
	public string? PlayerAvatarUrl { get; init; }

	/// <summary>
	/// The player's current chip stack.
	/// </summary>
	public int Chips { get; init; }

	/// <summary>
	/// Whether the player has indicated they are ready to play.
	/// </summary>
	public bool IsReady { get; init; }

	/// <summary>
	/// Whether the player has folded this hand.
	/// </summary>
	public bool IsFolded { get; init; }

	/// <summary>
	/// Whether the player is all-in.
	/// </summary>
	public bool IsAllIn { get; init; }

	/// <summary>
	/// Whether the player is disconnected.
	/// </summary>
	public bool IsDisconnected { get; init; }

	/// <summary>
	/// Whether the player is sitting out (not participating in the current hand).
	/// </summary>
	public bool IsSittingOut { get; init; }

	/// <summary>
	/// The reason why the player is sitting out, if applicable.
	/// Examples: "Insufficient chips", "Voluntarily sitting out".
	/// </summary>
	public string? SittingOutReason { get; init; }

	/// <summary>
	/// The player's current bet amount in this betting round.
	/// </summary>
	public int CurrentBet { get; init; }

	/// <summary>
	/// The cards visible at this seat (face-down placeholders for other players).
	/// </summary>
	public IReadOnlyList<CardPublicDto> Cards { get; init; } = [];
}

/// <summary>
/// Public representation of a card. When face-down, Rank and Suit are null.
/// </summary>
public sealed record CardPublicDto
{
	/// <summary>
	/// Whether the card is face-up and visible to all players.
	/// </summary>
	public bool IsFaceUp { get; init; }

	/// <summary>
	/// The rank of the card (e.g., "A", "K", "10"). Null when face-down.
	/// </summary>
	public string? Rank { get; init; }

	/// <summary>
	/// The suit of the card (e.g., "Hearts", "Spades"). Null when face-down.
	/// </summary>
	public string? Suit { get; init; }
}

/// <summary>
/// Public showdown information for displaying hand results.
/// </summary>
public sealed record ShowdownPublicDto
{
	/// <summary>
	/// The results for each player who participated in the showdown.
	/// </summary>
	public required IReadOnlyList<ShowdownPlayerResultDto> PlayerResults { get; init; }

	/// <summary>
	/// Whether the showdown is complete.
	/// </summary>
	public bool IsComplete { get; init; }
}

/// <summary>
/// Individual player result from a showdown.
/// </summary>
public sealed record ShowdownPlayerResultDto
{
	/// <summary>
	/// The player's name.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The player's first name.
	/// </summary>
	public string? PlayerFirstName { get; init; }

	/// <summary>
	/// The seat position of the player.
	/// </summary>
	public int SeatPosition { get; init; }

	/// <summary>
	/// The hand ranking description (e.g., "Full House", "Two Pair").
	/// </summary>
	public string? HandRanking { get; init; }

	/// <summary>
	/// Gets a textual description of the hand.
	/// </summary>
	public string? HandDescription { get; init; }
	

	/// <summary>
	/// The amount won by this player.
	/// </summary>
	public int AmountWon { get; init; }

	/// <summary>
	/// Whether this player won (or split) the pot.
	/// </summary>
	public bool IsWinner { get; init; }

	/// <summary>
	/// The player's cards (face-up for showdown display).
	/// </summary>
	public IReadOnlyList<CardPublicDto> Cards { get; init; } = [];
}
