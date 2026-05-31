using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Authoritative source of truth for whether a game session is able to continue to another hand.
/// </summary>
/// <remarks>
/// <para>
/// This policy intentionally lives in the application layer rather than in a mapper or projection.
/// Deciding whether a game can continue is a business rule, not a data-shaping concern, so mappers
/// must never re-derive it from partial persistence state. Query handlers compute the value through
/// this policy and pass the already-decided result into the mapper.
/// </para>
/// <para>
/// The rule mirrors the domain definition used by the <c>CardGames.Poker</c> game implementations
/// (<c>CanContinue()</c> returns <c>true</c> when at least two players still hold chips). If that rule
/// needs to change, change it here so every caller stays consistent.
/// </para>
/// </remarks>
public static class GameContinuationPolicy
{
	/// <summary>
	/// The minimum number of players that must still hold chips for a game to continue.
	/// </summary>
	public const int MinimumPlayersToContinue = 2;

	/// <summary>
	/// Determines whether a game can continue given the number of players that still hold chips.
	/// </summary>
	/// <param name="playersWithChips">The number of players whose chip stack is greater than zero.</param>
	/// <returns><c>true</c> when at least <see cref="MinimumPlayersToContinue"/> players still hold chips.</returns>
	public static bool CanContinue(int playersWithChips) =>
		playersWithChips >= MinimumPlayersToContinue;

	/// <summary>
	/// Determines whether a game can continue based on its players' chip stacks.
	/// </summary>
	/// <param name="players">The game's players.</param>
	/// <returns><c>true</c> when at least <see cref="MinimumPlayersToContinue"/> players still hold chips.</returns>
	public static bool CanContinue(IEnumerable<GamePlayer> players) =>
		CanContinue(players?.Count(gp => gp.ChipStack > 0) ?? 0);
}
