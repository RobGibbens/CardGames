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

				/// <summary>
				/// State for the Player vs Deck scenario (only one player stayed).
				/// Only populated when the game is in the PlayerVsDeck phase.
				/// </summary>
				public PlayerVsDeckStateDto? PlayerVsDeck { get; init; }

					/// <summary>
					/// The action timer state for the current player's turn.
					/// </summary>
					public ActionTimerStateDto? ActionTimer { get; init; }

					/// <summary>
						/// State for the all-in runout scenario (all players are all-in, remaining cards are being dealt).
						/// Only populated when dealing remaining streets after all players go all-in.
						/// </summary>
						public AllInRunoutStateDto? AllInRunout { get; init; }

						/// <summary>
						/// State for the chip check pause (game is paused waiting for players to add chips).
						/// Only populated when the game is paused for Kings and Lows pot coverage validation.
						/// </summary>
						public ChipCheckPauseStateDto? ChipCheckPause { get; init; }
					}

		/// <summary>
		/// State of the action timer for the current player's turn.
		/// </summary>
		public sealed record ActionTimerStateDto
		{
			/// <summary>
			/// The number of seconds remaining on the timer.
			/// </summary>
			public int SecondsRemaining { get; init; }

			/// <summary>
			/// The total duration of the timer in seconds.
			/// </summary>
			public int DurationSeconds { get; init; }

			/// <summary>
			/// The UTC timestamp when the timer was started.
			/// </summary>
			public DateTimeOffset StartedAtUtc { get; init; }

			/// <summary>
			/// The seat index of the player whose turn it is.
			/// </summary>
			public int PlayerSeatIndex { get; init; }

			/// <summary>
			/// Whether the timer is currently active.
			/// </summary>
			public bool IsActive { get; init; }
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
		/// Whether the player has already made their decision in the drop-or-stay phase.
		/// </summary>
		public bool HasDecidedDropOrStay { get; init; }

		/// <summary>
		/// The cards visible at this seat (face-down placeholders for other players).
		/// </summary>
		public IReadOnlyList<CardPublicDto> Cards { get; init; } = [];

		/// <summary>
		/// The last action performed by this player, for temporary display in the UI.
		/// </summary>
		public SeatLastActionDto? LastAction { get; init; }
	}

	/// <summary>
	/// Represents the last action performed by a player for display purposes.
	/// </summary>
	public sealed record SeatLastActionDto
	{
		/// <summary>
		/// The display description of the action (e.g., "Checked", "Raised 50", "Folded").
		/// </summary>
		public required string ActionDescription { get; init; }

		/// <summary>
		/// The UTC timestamp when the action was performed.
		/// </summary>
		public DateTimeOffset PerformedAtUtc { get; init; }
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

	/// <summary>
	/// The order in which this card was dealt.
	/// Used for displaying cards in deal order for stud-style games.
	/// </summary>
	public int DealOrder { get; init; }
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
	/// Player names who lost the showdown (for games with pot-matching rules).
	/// </summary>
	public IReadOnlyList<string>? Losers { get; init; }

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

	/// <summary>
	/// The zero-based indices of cards in the hand that make up the best 5-card hand.
	/// </summary>
	public IReadOnlyList<int>? BestCardIndexes { get; init; }
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

/// <summary>
/// State for the Player vs Deck scenario in Kings and Lows.
/// When only one player stays, they must face a hand dealt from the deck.
/// </summary>
public sealed record PlayerVsDeckStateDto
{
	/// <summary>
	/// The deck's hand cards, shown face-up to all players.
	/// </summary>
	public required IReadOnlyList<CardPublicDto> DeckCards { get; init; }

	/// <summary>
	/// The seat index of the player who makes the deck draw decision.
	/// This is the dealer, unless the dealer is the one who stayed.
	/// </summary>
	public int DecisionMakerSeatIndex { get; init; }

	/// <summary>
	/// The name of the player who makes the deck draw decision.
	/// </summary>
	public string? DecisionMakerName { get; init; }

	/// <summary>
	/// The first name of the player who makes the deck draw decision.
	/// </summary>
	public string? DecisionMakerFirstName { get; init; }

	/// <summary>
	/// Whether the deck has already drawn (completed its discard/draw).
	/// </summary>
	public bool HasDeckDrawn { get; init; }

	/// <summary>
	/// The name of the player who stayed (faces the deck).
	/// </summary>
	public string? StayingPlayerName { get; init; }

	/// <summary>
	/// The seat index of the player who stayed.
	/// </summary>
	public int StayingPlayerSeatIndex { get; init; }

	/// <summary>
	/// The staying player's cards, shown face-up to all players during the Player vs Deck phase.
	/// </summary>
	public IReadOnlyList<CardPublicDto> StayingPlayerCards { get; init; } = [];

	/// <summary>
	/// The staying player's hand description (e.g., "Two Pair, Kings and Sevens").
	/// </summary>
	public string? StayingPlayerHandDescription { get; init; }

		/// <summary>
		/// The deck's hand description (e.g., "Full House, Kings over Sevens").
		/// </summary>
		public string? DeckHandDescription { get; init; }
	}

	/// <summary>
	/// State for the all-in runout scenario in games like Seven Card Stud.
	/// When all players go all-in, remaining streets are dealt without betting,
	/// and this DTO tracks the animated dealing progress.
	/// </summary>
	public sealed record AllInRunoutStateDto
	{
		/// <summary>
		/// Whether the all-in runout is currently in progress.
		/// </summary>
		public bool IsActive { get; init; }

		/// <summary>
		/// The current street being dealt (e.g., "FourthStreet", "FifthStreet").
		/// </summary>
		public string? CurrentStreet { get; init; }

		/// <summary>
		/// A human-readable description of the current street (e.g., "Fourth Street").
		/// </summary>
		public string? CurrentStreetDescription { get; init; }

		/// <summary>
		/// The total number of streets to deal in the runout.
		/// </summary>
		public int TotalStreets { get; init; }

		/// <summary>
		/// The number of streets that have been dealt so far.
		/// </summary>
		public int StreetsDealt { get; init; }

		/// <summary>
		/// The cards that have been dealt in this runout, organized by player seat index.
		/// Key is the seat index, value is the list of cards dealt to that player during the runout.
		/// </summary>
		public IReadOnlyDictionary<int, IReadOnlyList<CardPublicDto>>? RunoutCardsBySeat { get; init; }

		/// <summary>
		/// The seat index of the player currently being dealt to (for animation).
		/// -1 if not currently dealing.
		/// </summary>
		public int CurrentDealingSeatIndex { get; init; }

		/// <summary>
		/// Whether the runout is complete and the showdown should begin.
		/// </summary>
		public bool IsComplete { get; init; }
	}

	/// <summary>
	/// State for the chip check pause in Kings and Lows.
	/// When a player cannot cover the pot, the game pauses to allow them to add chips.
	/// </summary>
	public sealed record ChipCheckPauseStateDto
	{
		/// <summary>
		/// Whether the game is currently paused for chip check.
		/// </summary>
		public bool IsPaused { get; init; }

		/// <summary>
		/// The UTC timestamp when the pause started.
		/// </summary>
		public DateTimeOffset? PauseStartedAt { get; init; }

		/// <summary>
		/// The UTC timestamp when the pause will expire.
		/// After this time, short players will be auto-dropped.
		/// </summary>
		public DateTimeOffset? PauseEndsAt { get; init; }

		/// <summary>
		/// The pot amount that players need to cover.
		/// </summary>
		public int PotAmountToCover { get; init; }

		/// <summary>
		/// List of players who are short on chips.
		/// </summary>
		public IReadOnlyList<ShortPlayerDto>? ShortPlayers { get; init; }
	}

	/// <summary>
	/// Information about a player who is short on chips.
	/// </summary>
	public sealed record ShortPlayerDto
	{
		/// <summary>
		/// The seat index of the player.
		/// </summary>
		public int SeatIndex { get; init; }

		/// <summary>
		/// The player's name.
		/// </summary>
		public required string PlayerName { get; init; }

		/// <summary>
		/// The player's first name, if available.
		/// </summary>
		public string? PlayerFirstName { get; init; }

		/// <summary>
		/// The player's current chip stack.
		/// </summary>
		public int CurrentChips { get; init; }

		/// <summary>
		/// The number of additional chips needed to cover the pot.
		/// </summary>
		public int ChipsNeeded { get; init; }
	}
