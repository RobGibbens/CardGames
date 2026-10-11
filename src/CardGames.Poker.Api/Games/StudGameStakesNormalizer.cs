using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Games;

/// <summary>
///     Derives fixed-limit stud stakes (<see cref="Game.BringIn" />, <see cref="Game.SmallBet" />,
///     <see cref="Game.BigBet" />) from ante, minimum bet, and rules metadata.
/// </summary>
internal static class StudGameStakesNormalizer
{
	public static bool TryGetHasBringIn(GameRules rules)
	{
		if (rules.SpecialRules is null ||
		    !rules.SpecialRules.TryGetValue("HasBringIn", out var value) ||
		    value is null)
			return false;

		return value switch
		{
			bool flag => flag,
			string text => bool.TryParse(text, out var parsed) && parsed,
			_ => bool.TryParse(value.ToString(), out var parsed) && parsed
		};
	}

	public static void ApplyToGame(Game game, GameRules rules, int ante, int minBet)
	{
		if (!IsStudStructure(rules))
			return;

		var smallBet = Math.Max(0, minBet);
		game.SmallBet = smallBet;
		game.BigBet = smallBet * 2;
		game.BringIn = TryGetHasBringIn(rules)
			? ante > 0 ? ante : Math.Max(1, smallBet / 2)
			: null;
	}

	/// <summary>
	///     Fills missing bring-in stakes on legacy stud rows that were created before stakes were persisted.
	/// </summary>
	public static async Task EnsureLegacyBringInAsync(
		CardsDbContext context,
		Game game,
		CancellationToken cancellationToken)
	{
		if (game.BringIn is > 0)
			return;

		var gameTypeCode = game.CurrentHandGameTypeCode;
		if (string.IsNullOrWhiteSpace(gameTypeCode))
			gameTypeCode = game.GameType?.Code;

		if (string.IsNullOrWhiteSpace(gameTypeCode) && game.GameTypeId is Guid gameTypeId)
			gameTypeCode = await context.GameTypes
				.Where(gameType => gameType.Id == gameTypeId)
				.Select(gameType => gameType.Code)
				.FirstOrDefaultAsync(cancellationToken);

		if (!PokerGameRulesRegistry.TryGet(gameTypeCode, out var rules) || rules is null)
			return;

		if (!TryGetHasBringIn(rules))
			return;

		ApplyToGame(game, rules, game.Ante ?? 0, game.MinBet ?? 0);
	}

	private static bool IsStudStructure(GameRules rules)
	{
		if (rules.Phases.Any(phase => phase.PhaseId == "ThirdStreet"))
			return true;

		return rules.SpecialRules?.ContainsKey("HasBringIn") == true;
	}
}
