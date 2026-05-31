using System;
using CardGames.Contracts.SignalR;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Features.Profile;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using Microsoft.EntityFrameworkCore;
using Entities = CardGames.Poker.Api.Data.Entities;
using static CardGames.Poker.Api.Services.TableVariantClassifier;
using static CardGames.Poker.Api.Services.TableCardMapper;
using static CardGames.Poker.Api.Services.TableHandEvaluators;


namespace CardGames.Poker.Api.Services;

public sealed partial class TableStateBuilder
{

	/// <summary>
	/// Builds the drawing configuration DTO from game rules.
	/// </summary>
	private static DrawingConfigDto? BuildDrawingConfigDto(GameRules? rules)
	{
		if (rules?.Drawing is null)
		{
			return null;
		}

		return new DrawingConfigDto
		{
			AllowsDrawing = rules.Drawing.AllowsDrawing,
			MaxDiscards = rules.Drawing.MaxDiscards,
			SpecialRules = rules.Drawing.SpecialRules,
			DrawingRounds = rules.Drawing.DrawingRounds
		};
	}

	/// <summary>
	/// Builds the special rules DTO from game rules, with dynamic wild card computation for Follow the Queen.
	/// </summary>
	private async Task<GameSpecialRulesDto?> BuildSpecialRulesDtoAsync(
		GameRules? rules,
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		if (rules?.SpecialRules is null || rules.SpecialRules.Count == 0)
		{
			return null;
		}

		var isFollowTheQueen = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
		var isPairPressure = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.PairPressureCode, StringComparison.OrdinalIgnoreCase);
		var isKlondike = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase);
		var isGoodBadUgly = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode, StringComparison.OrdinalIgnoreCase);

		// Dynamic-wild stud variants compute their active wild ranks from face-up cards.
		IReadOnlyList<string>? dynamicWildRanks = null;
		if (isFollowTheQueen)
		{
			dynamicWildRanks = await ComputeFollowTheQueenWildRanksAsync(game, cancellationToken);
		}
		else if (isPairPressure)
		{
			dynamicWildRanks = await ComputePairPressureWildRanksAsync(game, cancellationToken);
		}
		else if (isKlondike)
		{
			dynamicWildRanks = await ComputeKlondikeWildRanksAsync(game, cancellationToken);
		}
		else if (isGoodBadUgly)
		{
			dynamicWildRanks = await ComputeGoodBadUglyWildRanksAsync(game, cancellationToken);
		}

		return new GameSpecialRulesDto
		{
			HasDropOrStay = rules.SpecialRules.ContainsKey("DropOrStay"),
			HasKeepOrTrade = rules.SpecialRules.ContainsKey("KeepOrTrade"),
			HasPotMatching = rules.SpecialRules.ContainsKey("LosersMatchPot"),
			HasWildCards = rules.SpecialRules.ContainsKey("WildCards"),
			WildCardsDescription = rules.SpecialRules.TryGetValue("WildCards", out var wc)
				? wc?.ToString()
				: null,
			HasSevensSplit = rules.SpecialRules.ContainsKey("SevensSplit"),
			WildCardRules = BuildWildCardRulesDto(rules, dynamicWildRanks)
		};
	}

	/// <summary>
	/// Computes the current wild card ranks for Follow the Queen based on face-up cards dealt so far.
	/// Queens are always wild. The rank following the last face-up Queen is also wild.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeFollowTheQueenWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var sortedFaceUpCards = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

		var wildRanks = new List<string> { "Q" }; // Queens are always wild

		int? followingWildSymbol = null;
		for (var i = 0; i < sortedFaceUpCards.Count; i++)
		{
			if (sortedFaceUpCards[i].Symbol == Symbol.Queen)
			{
				if (i + 1 < sortedFaceUpCards.Count)
				{
					followingWildSymbol = (int)sortedFaceUpCards[i + 1].Symbol;
				}
				else
				{
					// Queen is the last face-up card, no following wild rank
					followingWildSymbol = null;
				}
			}
		}

		if (followingWildSymbol.HasValue)
		{
			var followRank = MapSymbolToRank((Entities.CardSymbol)followingWildSymbol.Value);
			if (followRank is not null)
			{
				wildRanks.Add(followRank);
			}
		}

		return wildRanks;
	}

	private async Task<IReadOnlyList<string>> ComputePairPressureWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var sortedFaceUpCards = await GetOrderedFaceUpCardsAsync(game, cancellationToken);
		var wildRanks = new PairPressureWildCardRules()
			.DetermineWildRanks(sortedFaceUpCards)
			.Select(rank => MapSymbolToRank((Entities.CardSymbol)rank))
			.Where(rank => rank is not null)
			.Cast<string>()
			.ToList();

		return wildRanks;
	}

	/// <summary>
	/// Computes the current wild card rank for Klondike.
	/// When the Klondike Card is revealed, all cards of that rank are wild.
	/// Returns empty if the card has not been revealed yet.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeKlondikeWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var klondikeCard = await _context.GameCards
			.Where(c => c.GameId == game.Id
				&& c.HandNumber == game.CurrentHandNumber
				&& c.DealtAtPhase == "KlondikeCard"
				&& c.IsVisible)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (klondikeCard is null)
		{
			return [];
		}

		var rank = MapSymbolToRank(klondikeCard.Symbol);
		return rank is not null ? [rank] : [];
	}

	/// <summary>
	/// Computes the current wild card rank for Good Bad Ugly.
	/// When The Good card is revealed, all cards of that rank are wild.
	/// Returns empty if The Good card has not been revealed yet.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeGoodBadUglyWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var goodCard = await _context.GameCards
			.Where(c => c.GameId == game.Id
				&& c.HandNumber == game.CurrentHandNumber
				&& c.DealtAtPhase == "TheGood"
				&& c.IsVisible)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (goodCard is null)
		{
			return [];
		}

		var rank = MapSymbolToRank(goodCard.Symbol);
		return rank is not null ? [rank] : [];
	}

	/// <summary>
	/// Builds the special rules DTO from game rules (static version for non-dynamic games).
	/// </summary>
	private static GameSpecialRulesDto? BuildSpecialRulesDto(GameRules? rules)
	{
		if (rules?.SpecialRules is null || rules.SpecialRules.Count == 0)
		{
			return null;
		}

		return new GameSpecialRulesDto
		{
			HasDropOrStay = rules.SpecialRules.ContainsKey("DropOrStay"),
			HasKeepOrTrade = rules.SpecialRules.ContainsKey("KeepOrTrade"),
			HasPotMatching = rules.SpecialRules.ContainsKey("LosersMatchPot"),
			HasWildCards = rules.SpecialRules.ContainsKey("WildCards"),
			WildCardsDescription = rules.SpecialRules.TryGetValue("WildCards", out var wc)
				? wc?.ToString()
				: null,
			HasSevensSplit = rules.SpecialRules.ContainsKey("SevensSplit"),
			WildCardRules = BuildWildCardRulesDto(rules)
		};
	}

	/// <summary>
	/// Builds structured wild card rules from game rules.
	/// </summary>
	/// <param name="rules">The game rules.</param>
	/// <param name="dynamicWildRanks">Optional pre-computed wild ranks for dynamic wild card games (e.g., Follow the Queen).</param>
	private static WildCardRulesDto? BuildWildCardRulesDto(GameRules? rules, IReadOnlyList<string>? dynamicWildRanks = null)
	{
		if (rules?.SpecialRules is null || !rules.SpecialRules.TryGetValue("WildCards", out var wildCardsValue))
		{
			return null;
		}

		var description = wildCardsValue?.ToString();
		var wildRanks = new List<string>();
		var specificCards = new List<string>();
		var lowestCardIsWild = false;

		// Parse known patterns into structured rules based on game type
		if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["2", "J"]);
			specificCards.Add("KD"); // King of Diamonds
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["3", "9"]);
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["3", "9"]);
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.Add("K");
			lowestCardIsWild = true;
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase))
		{
			// Follow the Queen: Queens are always wild, plus the dynamic "follow" rank
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
			else
			{
				wildRanks.Add("Q"); // Fallback: at minimum Queens are always wild
			}
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.PairPressureCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
		}

		return new WildCardRulesDto
		{
			WildRanks = wildRanks.Count > 0 ? wildRanks : null,
			SpecificCards = specificCards.Count > 0 ? specificCards : null,
			LowestCardIsWild = lowestCardIsWild,
			Description = description
		};
	}
}
