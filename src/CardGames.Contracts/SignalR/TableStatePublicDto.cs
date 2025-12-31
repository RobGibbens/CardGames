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
	/// The unique code identifying the game variant (e.g., "FIVECARDDRAW", "TWOSJACKSMANWITHTHEAXE").
	/// </summary>
	public string? GameTypeCode { get; init; }

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

	/// <summary>
	/// The category of the current phase (e.g., "Setup", "Betting", "Drawing", "Decision", "Resolution", "Special").
	/// Used by the UI to determine which overlay or panel to display.
	/// </summary>
	public string? CurrentPhaseCategory { get; init; }

	/// <summary>
	/// Whether the current phase requires player action.
	/// </summary>
	public bool CurrentPhaseRequiresAction { get; init; }

	/// <summary>
	/// Actions available in the current phase (e.g., ["Check", "Bet", "Call", "Raise", "Fold"]).
	/// </summary>
	public IReadOnlyList<string>? CurrentPhaseAvailableActions { get; init; }

	/// <summary>
	/// Configuration for drawing in the current game (if applicable).
	/// </summary>
	public DrawingConfigDto? DrawingConfig { get; init; }

	/// <summary>
	/// Whether the game has special rules (like Drop/Stay, Pot Matching, etc.).
	/// </summary>
	public GameSpecialRulesDto? SpecialRules { get; init; }
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

	/// <summary>
	/// Player names who won the sevens pool (had a natural pair of 7s).
	/// Only populated for games with the sevens half-pot rule.
	/// </summary>
	public IReadOnlyList<string>? SevensWinners { get; init; }

	/// <summary>
	/// Player names who won the high hand pool.
	/// Only populated for games with the sevens half-pot rule.
	/// </summary>
	public IReadOnlyList<string>? HighHandWinners { get; init; }

	/// <summary>
	/// Whether the sevens pool was rolled into the high hand pool
	/// because no players had a natural pair of 7s.
	/// </summary>
	public bool SevensPoolRolledOver { get; init; }
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
	/// The amount won by this player (total of sevens + high hand pools).
	/// </summary>
	public int AmountWon { get; init; }

	/// <summary>
	/// The amount won from the sevens pool (for games with sevens half-pot rule).
	/// </summary>
	public int SevensAmountWon { get; init; }

	/// <summary>
	/// The amount won from the high hand pool (for games with sevens half-pot rule).
	/// </summary>
	public int HighHandAmountWon { get; init; }

	/// <summary>
	/// Whether this player won (or split) the pot.
	/// </summary>
	public bool IsWinner { get; init; }

	/// <summary>
	/// Whether this player won the sevens pool (had a natural pair of 7s).
	/// </summary>
	public bool IsSevensWinner { get; init; }

	/// <summary>
	/// Whether this player won the high hand pool.
	/// </summary>
	public bool IsHighHandWinner { get; init; }

	/// <summary>
	/// The player's cards (face-up for showdown display).
	/// </summary>
	public IReadOnlyList<CardPublicDto> Cards { get; init; } = [];

	/// <summary>
	/// The zero-based indices of cards in the hand that are wild.
	/// Used by the UI to display wild card indicators.
	/// </summary>
	public IReadOnlyList<int>? WildCardIndexes { get; init; }
}

/// <summary>
/// Drawing configuration for the current game.
/// </summary>
public sealed record DrawingConfigDto
{
	/// <summary>
	/// Whether the game allows drawing cards.
	/// </summary>
	public bool AllowsDrawing { get; init; }

	/// <summary>
	/// Maximum number of cards that can be discarded.
	/// </summary>
	public int? MaxDiscards { get; init; }

	/// <summary>
	/// Special rules for discarding (e.g., "4 cards if holding an Ace").
	/// </summary>
	public string? SpecialRules { get; init; }

	/// <summary>
	/// Number of drawing rounds in the game.
	/// </summary>
	public int DrawingRounds { get; init; } = 1;
}

/// <summary>
/// Special rules for the current game.
/// </summary>
public sealed record GameSpecialRulesDto
{
	/// <summary>
	/// Whether the game has a Drop or Stay decision phase.
	/// </summary>
	public bool HasDropOrStay { get; init; }

	/// <summary>
	/// Whether losers must match the pot (e.g., Kings and Lows).
	/// </summary>
	public bool HasPotMatching { get; init; }

	/// <summary>
	/// Whether the game has wild cards.
	/// </summary>
	public bool HasWildCards { get; init; }

	/// <summary>
	/// Human-readable description of wild card rules.
	/// </summary>
	public string? WildCardsDescription { get; init; }

	/// <summary>
	/// Whether the game has sevens split pot rules.
	/// </summary>
	public bool HasSevensSplit { get; init; }

	/// <summary>
	/// Structured wild card rules for the game.
	/// </summary>
	public WildCardRulesDto? WildCardRules { get; init; }
}

/// <summary>
/// Defines which cards are wild in the current game.
/// </summary>
public sealed record WildCardRulesDto
{
	/// <summary>
	/// List of specific cards that are wild (e.g., "KD" for King of Diamonds).
	/// Format: "{Rank}{Suit}" where Rank is 2-10, J, Q, K, A and Suit is C, D, H, S.
	/// </summary>
	public IReadOnlyList<string>? SpecificCards { get; init; }

	/// <summary>
	/// List of ranks where all suits are wild (e.g., ["2", "J"] for all 2s and Jacks).
	/// </summary>
	public IReadOnlyList<string>? WildRanks { get; init; }

	/// <summary>
	/// Whether the player's lowest card is wild (for Kings and Lows).
	/// </summary>
	public bool LowestCardIsWild { get; init; }

	/// <summary>
	/// Human-readable description for UI display.
	/// </summary>
	public string? Description { get; init; }
}
