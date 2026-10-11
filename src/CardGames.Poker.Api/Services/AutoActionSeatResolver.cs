using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Authoritative seat that an expired action timer is allowed to act for.
/// </summary>
internal enum AutoActionTargetKind
{
	/// <summary>A single player must act in a betting round or other single-actor phase.</summary>
	Betting,

	/// <summary>A single player must act in a draw or discard phase.</summary>
	Draw,

	/// <summary>Every pending player acts together. Seat is not meaningful.</summary>
	Simultaneous,

	/// <summary>The phase does not accept a player action.</summary>
	None
}

/// <summary>
///     Result of resolving who must act for the current phase.
/// </summary>
/// <param name="Kind">How the phase expects a player action.</param>
/// <param name="SeatIndex">Required actor seat, or -1 when no single seat applies.</param>
/// <param name="BettingRoundId">Active betting round, when the seat came from one.</param>
/// <param name="UsedCurrentPlayerIndexFallback">
///     True when a betting phase had no incomplete round and the seat came from <see cref="Game.CurrentPlayerIndex" />.
/// </param>
internal sealed record AutoActionTarget(
	AutoActionTargetKind Kind,
	int SeatIndex,
	Guid? BettingRoundId,
	bool UsedCurrentPlayerIndexFallback = false);

/// <summary>
///     Resolves the seat that must act from the current game row and betting round.
/// </summary>
internal static class AutoActionSeatResolver
{
	/// <summary>
	///     Reloads the actor for <paramref name="game" /> using the same phase categories as game flow handlers.
	/// </summary>
	internal static async Task<AutoActionTarget> ResolveAsync(
		CardsDbContext context,
		Game game,
		GameRules rules,
		CancellationToken cancellationToken)
	{
		if (IsSimultaneousPhase(game.CurrentPhase, game.GameType?.Code))
			return new AutoActionTarget(AutoActionTargetKind.Simultaneous, -1, null);

		var phase = FindPhase(rules, game.CurrentPhase);
		if (IsCategory(phase, "Betting"))
			return await ResolveBettingActorAsync(context, game, cancellationToken);

		if (IsCategory(phase, "Drawing") && phase!.RequiresPlayerAction)
		{
			if (game.CurrentDrawPlayerIndex < 0)
				return new AutoActionTarget(AutoActionTargetKind.None, -1, null);

			return new AutoActionTarget(AutoActionTargetKind.Draw, game.CurrentDrawPlayerIndex, null);
		}

		if (phase?.RequiresPlayerAction == true && game.CurrentPlayerIndex >= 0)
			return new AutoActionTarget(AutoActionTargetKind.Betting, game.CurrentPlayerIndex, null);

		return new AutoActionTarget(AutoActionTargetKind.None, -1, null);
	}

	/// <summary>
	///     Latest incomplete betting round for the game's current hand.
	/// </summary>
	internal static Task<BettingRound?> FindActiveBettingRoundAsync(
		CardsDbContext context,
		Guid gameId,
		int handNumber,
		CancellationToken cancellationToken)
	{
		return context.BettingRounds
			.AsNoTracking()
			.Where(br => br.GameId == gameId && br.HandNumber == handNumber && !br.IsComplete)
			.OrderByDescending(br => br.RoundNumber)
			.ThenByDescending(br => br.StartedAt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	private static async Task<AutoActionTarget> ResolveBettingActorAsync(
		CardsDbContext context,
		Game game,
		CancellationToken cancellationToken)
	{
		var round = await FindActiveBettingRoundAsync(
			context, game.Id, game.CurrentHandNumber, cancellationToken);

		if (round is not null)
			return new AutoActionTarget(AutoActionTargetKind.Betting, round.CurrentActorIndex, round.Id);

		if (game.CurrentPlayerIndex >= 0)
			return new AutoActionTarget(
				AutoActionTargetKind.Betting,
				game.CurrentPlayerIndex,
				null,
				UsedCurrentPlayerIndexFallback: true);

		return new AutoActionTarget(AutoActionTargetKind.None, -1, null);
	}

	private static bool IsSimultaneousPhase(string phase, string? gameTypeCode)
	{
		if (phase.Equals("DropOrStay", StringComparison.OrdinalIgnoreCase))
			return true;

		return string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase)
		       && phase.Equals("DrawPhase", StringComparison.OrdinalIgnoreCase);
	}

	private static GamePhaseDescriptor? FindPhase(GameRules rules, string phase)
	{
		return rules.Phases.FirstOrDefault(p =>
			string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsCategory(GamePhaseDescriptor? phase, string category)
	{
		return string.Equals(phase?.Category, category, StringComparison.OrdinalIgnoreCase);
	}
}
