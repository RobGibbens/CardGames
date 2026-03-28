using System.Text.Json;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.InBetween;

/// <summary>
/// Manages In-Between-specific variant state stored in <see cref="Game.GameSettings"/>.
/// Tracks per-turn sub-phase, ace choice, first-orbit completion, and deck refresh signals.
/// </summary>
internal static class InBetweenVariantState
{
	/// <summary>
	/// Sub-phases within a single In-Between turn.
	/// </summary>
	internal enum TurnSubPhase
	{
		/// <summary>Awaiting first boundary card deal.</summary>
		AwaitingFirstBoundary,

		/// <summary>First boundary is an Ace — player must choose high or low.</summary>
		AwaitingAceChoice,

		/// <summary>Awaiting second boundary card deal.</summary>
		AwaitingSecondBoundary,

		/// <summary>Both boundaries dealt — player must bet or pass.</summary>
		AwaitingBetOrPass,

		/// <summary>Bet placed — awaiting third card reveal and resolution.</summary>
		AwaitingResolution,

		/// <summary>Turn resolved — ready to advance to next player.</summary>
		TurnComplete
	}

	/// <summary>
	/// The result of a resolved In-Between turn.
	/// </summary>
	internal enum TurnResult
	{
		None,
		Win,
		Lose,
		Post,
		Pass
	}

	internal static InBetweenState GetState(Game game)
	{
		if (string.IsNullOrWhiteSpace(game.GameSettings))
			return new InBetweenState();

		try
		{
			return JsonSerializer.Deserialize<InBetweenState>(game.GameSettings) ?? new InBetweenState();
		}
		catch (JsonException)
		{
			return new InBetweenState();
		}
	}

	internal static void SetState(Game game, InBetweenState state)
	{
		game.GameSettings = JsonSerializer.Serialize(state);
	}

	internal static void UpdateState(Game game, Action<InBetweenState> mutate)
	{
		var state = GetState(game);
		mutate(state);
		SetState(game, state);
	}

	/// <summary>
	/// Persistent state for an In-Between game, serialized to <see cref="Game.GameSettings"/>.
	/// </summary>
	internal sealed class InBetweenState
	{
		/// <summary>Current sub-phase of the active player's turn.</summary>
		public TurnSubPhase SubPhase { get; set; } = TurnSubPhase.AwaitingFirstBoundary;

		/// <summary>Whether the active player declared their Ace as high (true) or low (false). Null if no choice needed.</summary>
		public bool? AceIsHigh { get; set; }

		/// <summary>The bet amount the active player placed. 0 = pass.</summary>
		public int BetAmount { get; set; }

		/// <summary>The result of the current turn resolution.</summary>
		public TurnResult LastTurnResult { get; set; } = TurnResult.None;

		/// <summary>
		/// Set of seat positions that have completed their first turn in this game.
		/// While not all active players have completed their first turn, full-pot bets are disallowed.
		/// </summary>
		public HashSet<int> PlayersCompletedFirstTurn { get; set; } = [];

		/// <summary>Whether the deck was refreshed this turn (for UI toast notification).</summary>
		public bool DeckRefreshedThisTurn { get; set; }

		/// <summary>Description of the last turn result for action feed / toast.</summary>
		public string? LastTurnDescription { get; set; }
	}
}
