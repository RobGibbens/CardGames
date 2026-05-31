using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
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

	private async Task<ShowdownPublicDto?> BuildShowdownPublicDtoAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		Dictionary<string, UserProfile> userProfilesByEmail,
		CancellationToken cancellationToken)
	{
		var isTwosJacksAxe = IsTwosJacksAxeGame(game.GameType?.Code);
		var isGoodBadUgly = IsGoodBadUglyGame(game.GameType?.Code);
		var isHoldEm = IsHoldEmGame(game.GameType?.Code);
		var isHoldTheBaseball = IsHoldTheBaseballGame(game.GameType?.Code);
		var isOmaha = IsOmahaGame(game.GameType?.Code);
		var isBobBarker = IsBobBarkerGame(game.GameType?.Code);
		var isNebraska = IsNebraskaGame(game.GameType?.Code);
		var isSouthDakota = IsSouthDakotaGame(game.GameType?.Code);
		var isIrishHoldEm = IsIrishHoldEmGame(game.GameType?.Code);

		var isSevenCardStud = IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode)
			|| IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.RazzCode)
			|| IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.TollboothCode);
		var isRazz = IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.RazzCode);
		var isBaseball = IsBaseballGame(game.GameType?.Code);
		var isKingsAndLows = IsKingsAndLowsGame(game.GameType?.Code);
		var isFollowTheQueen = IsFollowTheQueenGame(game.GameType?.Code);
		var isPairPressure = IsPairPressureGame(game.GameType?.Code);
		var isScrewYourNeighbor = IsScrewYourNeighborGame(game.GameType?.Code);
		var isInBetween = IsInBetweenGame(game.GameType?.Code);
		var isStudStyleShowdown = isSevenCardStud || isBaseball || isFollowTheQueen || isPairPressure;
		var isTerminalScrewYourNeighborShowdown =
			isScrewYourNeighbor && string.Equals(game.CurrentPhase, "Ended", StringComparison.OrdinalIgnoreCase);

		// In-Between has no traditional showdown — skip evaluation entirely
		if (isInBetween)
		{
			return null;
		}

		if (game.CurrentPhase != "Showdown" &&
			game.CurrentPhase != "Complete" &&
			game.CurrentPhase != "PotMatching" &&
			!isTerminalScrewYourNeighborShowdown)
		{
			return null;
		}

		// Evaluate all hands for players who haven't folded
		// Use HandBase as the base type since all hand types inherit from it
		var playerHandEvaluations = new Dictionary<string, (HandBase hand, TwosJacksManWithTheAxeDrawHand? twosJacksHand, KingsAndLowsDrawHand? kingsAndLowsHand, SevenCardStudHand? studHand, GamePlayer gamePlayer, List<GameCard> cards, List<int> wildIndexes, List<int> bestCardIndexes)>();
		var showdownPlayers = gamePlayers
			.Where(gp => !gp.HasFolded)
			.ToList();

		// Good Bad Ugly: players have <=4 hole cards + community cards (The Good, The Bad, The Ugly)
		// Must be handled before the cards.Count >= 5 check since player-owned cards may be fewer than 5
		if (isGoodBadUgly)
		{
			var gbuCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			int? gbuWildRank = null;
			var goodCard = gbuCommunityCards.FirstOrDefault(c =>
				string.Equals(c.DealtAtPhase, "TheGood", StringComparison.OrdinalIgnoreCase) && c.IsVisible);
			if (goodCard is not null)
			{
				gbuWildRank = (int)goodCard.Symbol;
			}

			var gbuWildRules = new GoodBadUglyWildCardRules();
			var visibleCommunityCards = gbuCommunityCards
				.Where(c => c.IsVisible)
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var ownedCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();
				var allCoreCards = ownedCoreCards.Concat(visibleCommunityCards).ToList();
				var allDisplayCards = ownedCards.ToList();

				var gbuHand = new GoodBadUglyHand(allCoreCards, [], [], gbuWildRank, gbuWildRules);

				// Determine wild card indexes (within the player's owned cards only)
				var wildIndexes = new List<int>();
				if (gbuWildRank.HasValue)
				{
					for (int i = 0; i < ownedCoreCards.Count; i++)
					{
						if (ownedCoreCards[i].Value == gbuWildRank.Value)
						{
							wildIndexes.Add(i);
						}
					}
				}

				playerHandEvaluations[gp.Player.Name] = (gbuHand, null, null, null, gp, allDisplayCards, wildIndexes,
					GetCardIndexes(allCoreCards, gbuHand.EvaluatedBestCards));
			}
		}

		// Hold'Em: players have 2 hole cards + 5 shared community cards → best 5-of-7
		if (isHoldEm)
		{
			var holdemCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = holdemCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdemHand = new HoldemHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(holdemCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand from the 7 cards for highlighting
				var bestFive = FindBestFiveCardHand(allCoreCards);

				playerHandEvaluations[gp.Player.Name] = (holdemHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Hold the Baseball: Hold'em structure with 3s and 9s wild in hole and community cards.
		if (isHoldTheBaseball)
		{
			var holdTheBaseballCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = holdTheBaseballCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdTheBaseballHand = new HoldTheBaseballHand(holeCoreCards, communityCoreCards);

				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(holdTheBaseballCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				var wildIndexes = new List<int>();
				for (var i = 0; i < allCoreCards.Count; i++)
				{
					if (holdTheBaseballHand.WildCards.Contains(allCoreCards[i]))
					{
						wildIndexes.Add(i);
					}
				}

				playerHandEvaluations[gp.Player.Name] = (holdTheBaseballHand, null, null, null, gp, allDisplayCards, wildIndexes,
					GetCardIndexes(allCoreCards, holdTheBaseballHand.BestHandSourceCards));
			}
		}

		// Omaha: players have 4 hole cards + 5 shared community cards → best 5 using exactly 2 hole + 3 community
		if (isOmaha)
		{
			var omahaCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = omahaCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 4)
				{
					continue;
				}

				var omahaHand = new OmahaHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(omahaCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand using Omaha rules: exactly 2 hole + 3 community
				var bestFive = FindBestOmahaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (omahaHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		if (isBobBarker)
		{
			var bobBarkerCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded
					&& c.IsVisible)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = bobBarkerCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var selectedShowcaseDealOrder = BobBarkerVariantState.GetSelectedShowcaseDealOrder(gp);
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded
						&& c.HandNumber == game.CurrentHandNumber
						&& (selectedShowcaseDealOrder is null || c.DealOrder != selectedShowcaseDealOrder.Value))
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 4)
				{
					continue;
				}

				var bobBarkerHand = new BobBarkerHand(holeCoreCards, communityCoreCards);
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(bobBarkerCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();
				var bestFive = FindBestOmahaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (bobBarkerHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Nebraska: players have 5 hole cards + 5 shared community cards → best 5 using exactly 3 hole + 2 community
		if (isNebraska || isSouthDakota)
		{
			var nebraskaCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = nebraskaCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 5)
				{
					continue;
				}

				var nebraskaHand = new NebraskaHand(holeCoreCards, communityCoreCards);

				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(nebraskaCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				var bestFive = FindBestNebraskaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (nebraskaHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Irish Hold'Em: post-discard players have 2 hole cards + 5 community → best 5-of-7 (same as Hold'Em)
		if (isIrishHoldEm)
		{
			var irishCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = irishCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdemHand = new HoldemHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(irishCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand from the 7 cards for highlighting
				var bestFive = FindBestFiveCardHand(allCoreCards);

				playerHandEvaluations[gp.Player.Name] = (holdemHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
		{
			// Skip if already evaluated (e.g., by GBU-specific handling above)
			if (playerHandEvaluations.ContainsKey(gp.Player.Name))
			{
				continue;
			}

			var filteredCards = gp.Cards
				.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber);
			var cards = OrderCardsForDisplay(filteredCards, isStudStyleShowdown).ToList();

			if (cards.Count >= 5)
			{
				var coreCards = cards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (isTwosJacksAxe)
				{
					var wildHand = new TwosJacksManWithTheAxeDrawHand(coreCards);
					// Find wild card indexes
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (TwosJacksManWithTheAxeWildCardRules.IsWild(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (wildHand, wildHand, null, null, gp, cards, wildIndexes, Enumerable.Range(0, cards.Count).ToList());
				}
				else if (isBaseball)
				{
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					var allHoleCards = holeCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					var baseballHand = new BaseballHand(allHoleCards, openCards, []);
					var wildCards = baseballHand.WildCards;
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (wildCards.Contains(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (baseballHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, baseballHand.BestHandSourceCards));
				}
				else if (isFollowTheQueen)
				{
					// Follow The Queen: Similar to Seven Card Stud but with wild cards
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					var initialHoleCards = holeCards.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					// Get face-up cards for wild card determination
					var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

					if (initialHoleCards.Count == 2 && openCards.Count <= 4 && holeCards.Count >= 3)
					{
						var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
						var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
						var wildCards = ftqHand.WildCards;
						var wildIndexes = new List<int>();
						for (int i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}
						playerHandEvaluations[gp.Player.Name] = (ftqHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, ftqHand.BestHandSourceCards));
					}
					else if (initialHoleCards.Count >= 2)
					{
						// Partial hand (before 7th street)
						// For Follow The Queen, we must use the specific hand type to get wild card logic
						// The downCard parameter is nullable/optional in our modified constructor
						var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, null, faceUpCardsInOrder);
						var wildCards = ftqHand.WildCards;
						var wildIndexes = new List<int>();
						for (int i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}
						playerHandEvaluations[gp.Player.Name] = (ftqHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, ftqHand.BestHandSourceCards));
					}
				}
				else if (isPairPressure)
				{
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					var initialHoleCards = holeCards.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

					if (initialHoleCards.Count >= 2)
					{
						var downCard = holeCards.Count >= 3
							? new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol)
							: null;
						var pairPressureHand = new PairPressureHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
						var wildCards = pairPressureHand.WildCards;
						var wildIndexes = new List<int>();
						for (var i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}

						playerHandEvaluations[gp.Player.Name] = (pairPressureHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, pairPressureHand.BestHandSourceCards));
					}
				}
				else if (isSevenCardStud)
				{
					// Seven Card Stud: 2 hole cards + 4 board cards + 1 down card = 7 cards
					// Hole cards are Location == Hole, board cards are Location == Board
					// The final hole card (seventh street) is the down card
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					// For Seven Card Stud: first 2 hole cards are initial hole cards, 
					// last hole card (if 3 exist) is the seventh street down card
					var initialHoleCards = holeCards.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					// SevenCardStudHand requires exactly 2 hole cards, up to 4 open cards, and 1 down card
					if (initialHoleCards.Count == 2 && openCards.Count <= 4 && holeCards.Count >= 3)
					{
						var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
						if (isRazz)
						{
							var razzHand = new RazzHand(initialHoleCards, openCards, [downCard]);
							playerHandEvaluations[gp.Player.Name] = (razzHand, null, null, null, gp, cards, [], GetCardIndexes(coreCards, razzHand.GetBestLowHand()));
						}
						else
						{
							var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
							playerHandEvaluations[gp.Player.Name] = (studHand, null, null, studHand, gp, cards, [], GetCardIndexes(coreCards, studHand.GetBestHand()));
						}
					}
				}
				else if (isKingsAndLows)
				{
					var kingsAndLowsHand = new KingsAndLowsDrawHand(coreCards);
					// Find wild card indexes using Kings and Lows rules (Kings are wild, plus lowest non-King cards)
					var wildCards = kingsAndLowsHand.WildCards;
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (wildCards.Contains(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (kingsAndLowsHand, null, kingsAndLowsHand, null, gp, cards, wildIndexes, Enumerable.Range(0, cards.Count).ToList());
				}
				else
				{
					var drawHand = new DrawHand(coreCards);
					playerHandEvaluations[gp.Player.Name] = (drawHand, null, null, null, gp, cards, [], Enumerable.Range(0, cards.Count).ToList());
				}
			}
		}

		// Determine winners
		var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensPoolRolledOver = false;

		// Extract actual payouts from pots if they have been awarded
		var actualPayouts = new Dictionary<string, (int Total, int Sevens, int High, int Showcase)>(StringComparer.OrdinalIgnoreCase);
		var awardedHandPots = game.Pots
			.Where(p => p.HandNumber == game.CurrentHandNumber && p.IsAwarded)
			.ToList();

		foreach (var pot in awardedHandPots)
		{
			if (string.IsNullOrWhiteSpace(pot.WinnerPayouts))
			{
				continue;
			}

			try
			{
				using var doc = JsonDocument.Parse(pot.WinnerPayouts);
				foreach (var element in doc.RootElement.EnumerateArray())
				{
					if (element.TryGetProperty("playerName", out var nameProp))
					{
						var name = nameProp.GetString();
						if (string.IsNullOrEmpty(name))
						{
							continue;
						}

						int amount = 0;
						if (element.TryGetProperty("amount", out var amountProp))
						{
							amount = amountProp.GetInt32();
						}

						int sevensAmount = 0;
						if (element.TryGetProperty("sevensAmount", out var sProp))
						{
							sevensAmount = sProp.GetInt32();
						}

						int highAmount = 0;
						if (element.TryGetProperty("highHandAmount", out var hProp))
						{
							highAmount = hProp.GetInt32();
						}

						int showcaseAmount = 0;
						if (element.TryGetProperty("showcaseAmount", out var showcaseProp))
						{
							showcaseAmount = showcaseProp.GetInt32();
						}

						if (actualPayouts.TryGetValue(name, out var existing))
						{
							actualPayouts[name] = (existing.Total + amount, existing.Sevens + sevensAmount, existing.High + highAmount, existing.Showcase + showcaseAmount);
						}
						else
						{
							actualPayouts[name] = (amount, sevensAmount, highAmount, showcaseAmount);
						}
					}
				}
			}
			catch (JsonException)
			{
				// Skip invalid JSON
			}
		}

		if (playerHandEvaluations.Count > 0)
		{
			// For Good Bad Ugly, filter out players eliminated by The Ugly before determining winners
			if (isGoodBadUgly)
			{
				var eligibleEvaluations = playerHandEvaluations
					.Where(kvp => !string.Equals(kvp.Value.gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

				if (eligibleEvaluations.Count > 0)
				{
					var maxEligibleStrength = eligibleEvaluations.Values.Max(h => h.hand.Strength);
					foreach (var kvp in eligibleEvaluations.Where(k => k.Value.hand.Strength == maxEligibleStrength))
					{
						highHandWinners.Add(kvp.Key);
					}
				}
				else
				{
					// All remaining players were eliminated by The Ugly: split among all
					foreach (var kvp in playerHandEvaluations)
					{
						highHandWinners.Add(kvp.Key);
					}
				}
			}
			else
			{
				// Determine high hand winners (highest hand strength)
				var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
				foreach (var kvp in playerHandEvaluations.Where(k => k.Value.hand.Strength == maxStrength))
				{
					highHandWinners.Add(kvp.Key);
				}
			}

			// For Twos/Jacks/Axe, also determine sevens winners
			if (isTwosJacksAxe)
			{
				foreach (var kvp in playerHandEvaluations.Where(k => k.Value.twosJacksHand?.HasNaturalPairOfSevens() == true))
				{
					sevensWinners.Add(kvp.Key);
				}
				sevensPoolRolledOver = sevensWinners.Count == 0;
			}
		}
		else if (isScrewYourNeighbor)
		{
			// SYN: each player has exactly 1 card. Lowest card value loses (Ace=1, King=13).
			// Players who do NOT have the lowest value are winners.
			var synActivePlayers = showdownPlayers;

			var synHandCards = await _context.GameCards
				.Where(gc => gc.GameId == game.Id
					&& gc.HandNumber == game.CurrentHandNumber
					&& gc.GamePlayerId != null
					&& gc.Location == CardLocation.Hand
					&& !gc.IsDiscarded)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var synPlayerCards = new Dictionary<Guid, GameCard>();
			foreach (var card in synHandCards)
			{
				if (card.GamePlayerId.HasValue)
				{
					synPlayerCards[card.GamePlayerId.Value] = card;
				}
			}

			var synPlayerValues = new List<(GamePlayer Player, int CardValue)>();
			foreach (var player in synActivePlayers)
			{
				if (synPlayerCards.TryGetValue(player.Id, out var card))
				{
					var value = ScrewYourNeighborFlowHandler.GetScrewYourNeighborCardValue(card.Symbol);
					synPlayerValues.Add((player, value));
				}
			}

			if (synPlayerValues.Count > 0)
			{
				var lowestValue = synPlayerValues.Min(pv => pv.CardValue);
				foreach (var pv in synPlayerValues.Where(pv => pv.CardValue != lowestValue))
				{
					highHandWinners.Add(pv.Player.Player.Name);
				}
			}

			showdownPlayers = synPlayerValues
				.Select(pv => pv.Player)
				.ToList();
		}
		else if (gamePlayers.Count(gp => !gp.HasFolded) == 1)
		{
			// Only one player remaining (won by fold)
			var winner = gamePlayers.First(gp => !gp.HasFolded);
			highHandWinners.Add(winner.Player.Name);
		}

		// Combined winners for IsWinner flag
		var showcaseWinners = isBobBarker
			? actualPayouts.Where(kvp => kvp.Value.Showcase > 0).Select(kvp => kvp.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var mainHandWinners = isBobBarker && actualPayouts.Any(kvp => kvp.Value.High > 0)
			? actualPayouts.Where(kvp => kvp.Value.High > 0).Select(kvp => kvp.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
			: highHandWinners;
		var allWinners = mainHandWinners.Union(sevensWinners).Union(showcaseWinners).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var allLosers = isKingsAndLows
			? gamePlayers.Where(gp => !gp.HasFolded && !highHandWinners.Contains(gp.Player.Name))
				.Select(gp => gp.Player.Name)
				.ToList()
			: null;

		var bobBarkerDealerCard = isBobBarker
			? await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken)
			: null;

		// Build player results
		var playerResults = showdownPlayers
			.Select(gp =>
			{
				var isWinner = allWinners.Contains(gp.Player.Name);
				var isSevensWinner = sevensWinners.Contains(gp.Player.Name);
				var isHighHandWinner = mainHandWinners.Contains(gp.Player.Name);
				var isShowcaseWinner = showcaseWinners.Contains(gp.Player.Name);
				string? handRanking = null;
				List<int>? wildIndexes = null;
				List<int>? bestCardIndexes = null;

				if (playerHandEvaluations.TryGetValue(gp.Player.Name, out var eval))
				{
					handRanking = eval.hand.Type.ToString();
					wildIndexes = eval.wildIndexes.Count > 0 ? eval.wildIndexes : null;
					bestCardIndexes = eval.bestCardIndexes.Count > 0 ? eval.bestCardIndexes : null;
				}

				userProfilesByEmail.TryGetValue(gp.Player.Email ?? string.Empty, out var userProfile);

				actualPayouts.TryGetValue(gp.Player.Name, out var payouts);
				var selectedShowcaseDealOrder = isBobBarker
					? BobBarkerVariantState.GetSelectedShowcaseDealOrder(gp)
					: null;
				var showcaseCard = isBobBarker && selectedShowcaseDealOrder.HasValue
					? gp.Cards.FirstOrDefault(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber && c.DealOrder == selectedShowcaseDealOrder.Value)
					: null;

				return new ShowdownPlayerResultDto
				{
					PlayerName = gp.Player.Name,
					PlayerFirstName = GetPlayerFirstName(gp, userProfilesByEmail),
					SeatPosition = gp.SeatPosition,
					HandRanking = handRanking,
					HandDescription = isGoodBadUgly && string.Equals(gp.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase)
						? "Dead Hand (The Ugly)"
						: playerHandEvaluations.TryGetValue(gp.Player.Name, out var e)
							? HandDescriptionFormatter.GetHandDescription(e.hand)
							: null,
					AmountWon = payouts.Total,
					SevensAmountWon = payouts.Sevens,
					HighHandAmountWon = payouts.High,
					ShowcaseAmountWon = payouts.Showcase,
					IsWinner = isWinner,
					IsSevensWinner = isSevensWinner,
					IsHighHandWinner = isHighHandWinner,
					IsShowcaseWinner = isShowcaseWinner,
					WildCardIndexes = wildIndexes,
					BestCardIndexes = bestCardIndexes,
					ShowcaseCard = showcaseCard is null
						? null
						: new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(showcaseCard.Symbol),
							Suit = GetCardSuitString(showcaseCard.Suit),
							DealOrder = showcaseCard.DealOrder
						},
					ShowcaseCardValue = showcaseCard is null
						? null
						: GetBobBarkerCardValue(showcaseCard.Symbol, bobBarkerDealerCard?.Symbol == Entities.CardSymbol.Ace),
					Cards = OrderCardsForDisplay(
							gp.Cards.Where(c => !c.IsDiscarded
								&& c.HandNumber == game.CurrentHandNumber
								&& (!isBobBarker || selectedShowcaseDealOrder is null || c.DealOrder != selectedShowcaseDealOrder.Value)),
							isStudStyleShowdown)
						.Select(c => new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(c.Symbol),
							Suit = GetCardSuitString(c.Suit),
							DealOrder = c.DealOrder
						})
						.ToList()
				};
			})
			.OrderByDescending(r => r.IsWinner)
			.ThenByDescending(r => playerHandEvaluations.TryGetValue(r.PlayerName, out var e) ? e.hand.Strength : 0)
			.ToList();

		// For Kings and Lows player-vs-deck scenario, add the deck as a player in the results
		if (isKingsAndLows && playerResults.Count == 1)
		{
			var playerResult = playerResults[0];
			var playerStrength = playerHandEvaluations.Values.FirstOrDefault().hand?.Strength ?? 0;
			var deckOutcome = await BuildKingsAndLowsDeckOutcomeAsync(
				game,
				playerResult.PlayerName,
				playerStrength,
				cancellationToken);

			if (deckOutcome is not null)
			{
				if (deckOutcome.PlayerWins)
				{
					playerResults[0] = playerResult with { IsWinner = true };
					highHandWinners.Add(playerResult.PlayerName);
				}
				else
				{
					playerResults[0] = playerResult with { IsWinner = false };
					highHandWinners.Clear();
					allLosers = deckOutcome.Losers;
				}

				playerResults.Add(deckOutcome.DeckResult);
			}
		}

		return new ShowdownPublicDto
		{
			PlayerResults = playerResults,
			IsComplete = game.CurrentPhase == "Complete" || isTerminalScrewYourNeighborShowdown,
			SevensWinners = isTwosJacksAxe ? sevensWinners.ToList() : null,
			HighHandWinners = isTwosJacksAxe ? highHandWinners.ToList() : null,
			Losers = allLosers,
			SevensPoolRolledOver = sevensPoolRolledOver,
			BobBarker = isBobBarker && bobBarkerDealerCard is not null
				? new BobBarkerShowdownStateDto
				{
					DealerCard = new CardPublicDto
					{
						IsFaceUp = true,
						Rank = MapSymbolToRank(bobBarkerDealerCard.Symbol),
						Suit = GetCardSuitString(bobBarkerDealerCard.Suit),
						DealOrder = bobBarkerDealerCard.DealOrder
					},
					DealerCardValue = GetBobBarkerCardValue(bobBarkerDealerCard.Symbol, bobBarkerDealerCard.Symbol == Entities.CardSymbol.Ace),
					MainHandWinners = mainHandWinners.OrderBy(name => name).ToList(),
					ShowcaseWinners = showcaseWinners.OrderBy(name => name).ToList()
				}
				: null
		};
	}

	private async Task<KingsAndLowsDeckOutcome?> BuildKingsAndLowsDeckOutcomeAsync(
		Game game,
		string playerName,
		long playerStrength,
		CancellationToken cancellationToken)
	{
		var deckCards = await _context.GameCards
			.Where(c => c.GameId == game.Id &&
						c.HandNumber == game.CurrentHandNumber &&
						!c.IsDiscarded &&
						c.GamePlayerId == null &&
						c.Location == CardLocation.Board)
			.OrderBy(c => c.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		if (deckCards.Count < 5)
		{
			return null;
		}

		var deckCoreCards = deckCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();
		var deckHand = new KingsAndLowsDrawHand(deckCoreCards);

		var deckWildCards = deckHand.WildCards;
		var deckWildIndexes = new List<int>();
		for (var i = 0; i < deckCoreCards.Count; i++)
		{
			if (deckWildCards.Contains(deckCoreCards[i]))
			{
				deckWildIndexes.Add(i);
			}
		}

		var playerWins = playerStrength >= deckHand.Strength;
		var deckWins = !playerWins;

		var deckResult = new ShowdownPlayerResultDto
		{
			PlayerName = "The Deck",
			PlayerFirstName = "Deck",
			SeatPosition = -1,
			HandRanking = deckHand.Type.ToString(),
			HandDescription = HandDescriptionFormatter.GetHandDescription(deckHand),
			AmountWon = 0,
			SevensAmountWon = 0,
			HighHandAmountWon = 0,
			IsWinner = deckWins,
			IsSevensWinner = false,
			IsHighHandWinner = deckWins,
			WildCardIndexes = deckWildIndexes.Count > 0 ? deckWildIndexes : null,
			Cards = deckCards
				.Select(c => new CardPublicDto
				{
					IsFaceUp = true,
					Rank = MapSymbolToRank(c.Symbol),
					Suit = c.Suit.ToString()
				})
				.ToList()
		};

		return new KingsAndLowsDeckOutcome(
			playerWins,
			deckResult,
			deckWins ? [playerName] : null);
	}

	/// <summary>
	/// Retrieves hand history entries for the dashboard.
	/// </summary>
	private async Task<List<CardGames.Contracts.SignalR.HandHistoryEntryDto>> GetHandHistoryEntriesAsync(
		Guid gameId,
		Guid? currentUserPlayerId,
		int take,
		CancellationToken cancellationToken)
	{
		var histories = await _context.HandHistories
			.Include(h => h.Winners)
				.ThenInclude(w => w.Player)
			.Include(h => h.PlayerResults)
			.Where(h => h.GameId == gameId)
			.OrderByDescending(h => h.CompletedAtUtc)
			.Take(take)
			.AsSplitQuery()
			.AsNoTracking()
				.ToListAsync(cancellationToken);

		// Get all player IDs from the histories
		var allPlayerIds = histories
			.SelectMany(h => h.PlayerResults)
			.Select(pr => pr.PlayerId)
			.Distinct()
			.ToList();

		_logger.LogInformation("[HANDHISTORY-NAMES] Loading player names/emails for {PlayerCount} player IDs: {PlayerIds}",
			allPlayerIds.Count, string.Join(", ", allPlayerIds.Take(5)));

		// Load all players separately to get Names and Emails
		var playersData = await _context.Players
			.Where(p => allPlayerIds.Contains(p.Id))
			.AsNoTracking()
			.Select(p => new { p.Id, p.Name, p.Email })
			.ToListAsync(cancellationToken);

		var playersByIdLookup = playersData.ToDictionary(p => p.Id, p => (Name: p.Name, Email: p.Email));

		_logger.LogInformation("[HANDHISTORY-NAMES] Loaded {PlayerCount} player names from Players table", playersByIdLookup.Count);
		foreach (var kvp in playersByIdLookup)
		{
			_logger.LogInformation("[HANDHISTORY-NAMES] Player {PlayerId} -> Name: '{PlayerName}', Email: '{Email}'", kvp.Key, kvp.Value.Name, kvp.Value.Email);
		}

		// Cards are now stored in HandHistoryPlayerResult.ShowdownCards (JSON)
		// No need to query GameCards table
		_logger.LogInformation("[HANDHISTORY-CARDS] Cards will be loaded from stored ShowdownCards in HandHistoryPlayerResult");

		var allEmails = playersData
			.Select(p => p.Email)
			.Concat(histories.SelectMany(h => h.Winners).Select(w => w.Player.Email))
			.Where(email => !string.IsNullOrWhiteSpace(email))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var userFirstNamesByEmail = allEmails.Count == 0
			? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
			: await _context.Users
				.AsNoTracking()
				.Where(u => u.Email != null && allEmails.Contains(u.Email))
				.Select(u => new { Email = u.Email!, u.FirstName })
				.ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

		_logger.LogInformation("GetHandHistoryEntriesAsync: Found {Count} histories for game {GameId}, currentUserPlayerId={PlayerId}",
			histories.Count, gameId, currentUserPlayerId);

		return histories.Select(h =>
		{
			_logger.LogInformation("HandHistory {HandNumber}: Winners.Count={WinnerCount}, PlayerResults.Count={PlayerResultCount}",
				h.HandNumber, h.Winners.Count, h.PlayerResults.Count);

			// Get winner display
			string GetWinnerFirstNameOrFallback()
			{
				var firstWinner = h.Winners.First();
				var email = firstWinner.Player.Email;

				if (!string.IsNullOrWhiteSpace(email) &&
					userFirstNamesByEmail.TryGetValue(email, out var firstName) &&
					!string.IsNullOrWhiteSpace(firstName))
				{
					return firstName;
				}

				return firstWinner.PlayerName;
			}

			var winnerDisplay = h.Winners.Count switch
			{
				0 => "Unknown",
				1 => GetWinnerFirstNameOrFallback(),
				_ => $"{GetWinnerFirstNameOrFallback()} +{h.Winners.Count - 1}"
			};

			_logger.LogInformation("HandHistory {HandNumber}: winnerDisplay='{WinnerDisplay}'", h.HandNumber, winnerDisplay);

			var totalWinnings = h.Winners.Sum(w => w.AmountWon);

			// Map all player results
			var playerResults = h.PlayerResults
				.OrderBy(pr => pr.SeatPosition)
				.Select(pr =>
				{
					// Get player's actual name from Players lookup, fallback to stored name if not available
					var foundInLookup = playersByIdLookup.TryGetValue(pr.PlayerId, out var playerInfo);
					var playerName = foundInLookup ? playerInfo.Name : pr.PlayerName;

					// Try getting first name (real name) via email if available
					if (foundInLookup &&
						!string.IsNullOrWhiteSpace(playerInfo.Email) &&
						userFirstNamesByEmail.TryGetValue(playerInfo.Email, out var firstName) &&
						!string.IsNullOrWhiteSpace(firstName))
					{
						playerName = firstName;
					}

					if (!foundInLookup)
					{
						_logger.LogWarning("[HANDHISTORY-NAMES] Player ID {PlayerId} not found in lookup, using stored name: '{StoredName}'",
							pr.PlayerId, pr.PlayerName);
					}

					// Get cards for this player if they reached showdown
					List<string>? visibleCards = null;
					if (pr.ReachedShowdown && !string.IsNullOrWhiteSpace(pr.ShowdownCards))
					{
						try
						{
							visibleCards = System.Text.Json.JsonSerializer.Deserialize<List<string>>(pr.ShowdownCards);
							if (visibleCards != null && visibleCards.Any())
							{
								_logger.LogInformation("[HANDHISTORY-CARDS] ✓ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}): Found {CardCount} cards from ShowdownCards: {Cards}",
									h.HandNumber, playerName, pr.SeatPosition, visibleCards.Count, string.Join(", ", visibleCards));
							}
							else
							{
								_logger.LogWarning("[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}): ShowdownCards deserialized but empty",
									h.HandNumber, playerName, pr.SeatPosition);
							}
						}
						catch (System.Text.Json.JsonException ex)
						{
							_logger.LogError(ex, "[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}': Failed to deserialize ShowdownCards: {Json}",
								h.HandNumber, playerName, pr.ShowdownCards);
						}
					}
					else if (pr.ReachedShowdown)
					{
						_logger.LogWarning("[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}, PlayerId {PlayerId}): Reached showdown but ShowdownCards is null/empty",
							h.HandNumber, playerName, pr.SeatPosition, pr.PlayerId);
					}

					return new CardGames.Contracts.SignalR.PlayerHandResultDto
					{
						PlayerId = pr.PlayerId,
						PlayerName = playerName,
						SeatPosition = pr.SeatPosition,
						ResultType = pr.ResultType.ToString(),
						ResultLabel = pr.GetResultLabel(),
						NetAmount = pr.NetChipDelta,
						ReachedShowdown = pr.ReachedShowdown,
						VisibleCards = visibleCards
					};
				})
				.ToList();

			return new CardGames.Contracts.SignalR.HandHistoryEntryDto
			{
				HandNumber = h.HandNumber,
				CompletedAtUtc = h.CompletedAtUtc,
				WinnerName = winnerDisplay,
				AmountWon = totalWinnings,
				WinningHandDescription = h.WinningHandDescription,
				WonByFold = h.EndReason == Data.Entities.HandEndReason.FoldedToWinner,
				WinnerCount = h.Winners.Count,
				PlayerResults = playerResults
			};
		}).ToList();
	}

	/// <summary>
	/// Builds the Player vs Deck state for games where only one player stayed.
	/// </summary>
	private async Task<PlayerVsDeckStateDto?> BuildPlayerVsDeckStateAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		IReadOnlyDictionary<string, UserProfile> userProfilesByEmail,
		CancellationToken cancellationToken)
	{
		// Only build for PlayerVsDeck phase
		if (!string.Equals(game.CurrentPhase, "PlayerVsDeck", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// Get deck cards (cards with no GamePlayerId in this hand, on the Board)
		// The deck's hand is stored as cards with no GamePlayerId and Location = Board
		var deckCards = await _context.GameCards
			.Where(gc => gc.GameId == game.Id
					 && gc.GamePlayerId == null
					 && gc.HandNumber == game.CurrentHandNumber
					 && gc.Location == Entities.CardLocation.Board
					 && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Find the staying player
		var stayingPlayer = gamePlayers
			.FirstOrDefault(gp => gp.DropOrStayDecision == Entities.DropOrStayDecision.Stay);

		if (stayingPlayer is null)
		{
			_logger.LogWarning("No staying player found in PlayerVsDeck phase for game {GameId}", game.Id);
			return null;
		}

		// Determine decision maker: any active non-staying player, preferring the dealer.
		// The decision maker chooses which cards to discard from the deck's hand.
		var dealerSeatPosition = game.DealerPosition;
		var orderedPlayers = gamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		GamePlayer? decisionMaker = null;

		// Try dealer first (must be active, not sitting out, and not the staying player)
		var dealer = orderedPlayers.FirstOrDefault(gp => gp.SeatPosition == dealerSeatPosition);
		if (dealer is not null &&
			dealer.PlayerId != stayingPlayer.PlayerId &&
			dealer.Status == Entities.GamePlayerStatus.Active &&
			!dealer.IsSittingOut)
		{
			decisionMaker = dealer;
		}

		// If dealer can't be the decision maker, find the first eligible player
		// searching clockwise from the dealer position
		if (decisionMaker is null)
		{
			var dealerIndex = orderedPlayers.FindIndex(gp => gp.SeatPosition == dealerSeatPosition);
			if (dealerIndex < 0) dealerIndex = 0;

			for (int i = 1; i <= orderedPlayers.Count; i++)
			{
				var nextIndex = (dealerIndex + i) % orderedPlayers.Count;
				var candidate = orderedPlayers[nextIndex];
				if (candidate.PlayerId != stayingPlayer.PlayerId &&
					candidate.Status == Entities.GamePlayerStatus.Active &&
					!candidate.IsSittingOut)
				{
					decisionMaker = candidate;
					break;
				}
			}
		}

		// Fallback: if no other player found, the staying player makes the decision
		decisionMaker ??= stayingPlayer;

		// Get decision maker's first name from user profile
		string? decisionMakerFirstName = null;
		if (!string.IsNullOrWhiteSpace(decisionMaker.Player?.Email) &&
			userProfilesByEmail.TryGetValue(decisionMaker.Player.Email, out var profile))
		{
			decisionMakerFirstName = profile.FirstName;
		}

		// Check if deck has drawn (by checking if any cards were dealt after the initial 5)
		// For simplicity, we'll track this via a flag or by checking draw records
		// For now, check if deck has exactly 5 cards and they haven't been modified
		var hasDeckDrawn = game.CurrentPhase != "PlayerVsDeck"; // If we've moved past, it's drawn

		// Get the staying player's cards
		var stayingPlayerCards = await _context.GameCards
			.Where(gc => gc.GameId == game.Id
					 && gc.GamePlayerId == stayingPlayer.Id
					 && gc.HandNumber == game.CurrentHandNumber
					 && gc.Location == Entities.CardLocation.Hand
					 && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealtAt)
			.ThenBy(gc => gc.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Evaluate the staying player's hand for the description
		string? stayingPlayerHandDescription = null;
		if (stayingPlayerCards.Count >= 5)
		{
			try
			{
				var cards = stayingPlayerCards
					.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
					.ToList();
				var kingsAndLowsHand = new KingsAndLowsDrawHand(cards);
				stayingPlayerHandDescription = HandDescriptionFormatter.GetHandDescription(kingsAndLowsHand);
			}
			catch
			{
				// Ignore evaluation errors
			}
		}

		// Evaluate the deck's hand for the description
		string? deckHandDescription = null;
		if (deckCards.Count >= 5)
		{
			try
			{
				var cards = deckCards
					.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
					.ToList();
				var kingsAndLowsHand = new KingsAndLowsDrawHand(cards);
				deckHandDescription = HandDescriptionFormatter.GetHandDescription(kingsAndLowsHand);
			}
			catch
			{
				// Ignore evaluation errors
			}
		}

		return new PlayerVsDeckStateDto
		{
			DeckCards = deckCards.Select(c => new CardPublicDto
			{
				IsFaceUp = true,
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString()
			}).ToList(),
			DecisionMakerSeatIndex = decisionMaker.SeatPosition,
			DecisionMakerName = decisionMaker.Player?.Name,
			DecisionMakerFirstName = decisionMakerFirstName,
			HasDeckDrawn = hasDeckDrawn,
			StayingPlayerName = stayingPlayer.Player?.Name,
			StayingPlayerSeatIndex = stayingPlayer.SeatPosition,
			StayingPlayerCards = stayingPlayerCards.Select(c => new CardPublicDto
			{
				IsFaceUp = true,
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString()
			}).ToList(),
			StayingPlayerHandDescription = stayingPlayerHandDescription,
			DeckHandDescription = deckHandDescription
		};
	}

	/// <summary>
	/// Builds the All-In Runout state for games where all players went all-in
	/// and remaining streets were dealt without betting.
	/// </summary>
	private async Task<AllInRunoutStateDto?> BuildAllInRunoutStateAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		CancellationToken cancellationToken)
	{
		// Only build for Showdown phase
		if (!string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// Check if this is an all-in runout by reading from GameSettings
		if (string.IsNullOrEmpty(game.GameSettings))
		{
			return null;
		}

		try
		{
			using var settingsDoc = JsonDocument.Parse(game.GameSettings);
			var root = settingsDoc.RootElement;

			// Check for allInRunout flag
			if (!root.TryGetProperty("allInRunout", out var allInRunoutProp) ||
				!allInRunoutProp.GetBoolean())
			{
				return null;
			}

			// Verify this is for the current hand
			if (root.TryGetProperty("runoutHandNumber", out var handNumberProp) &&
				handNumberProp.GetInt32() != game.CurrentHandNumber)
			{
				return null;
			}

			// Get the streets that were dealt during the runout
			var runoutStreets = new List<string>();
			if (root.TryGetProperty("runoutStreets", out var streetsProp) &&
				streetsProp.ValueKind == JsonValueKind.Array)
			{
				foreach (var street in streetsProp.EnumerateArray())
				{
					runoutStreets.Add(street.GetString() ?? "");
				}
			}

			if (runoutStreets.Count == 0)
			{
				return null;
			}

			// Get the timestamp when the runout occurred
			DateTimeOffset? runoutTimestamp = null;
			if (root.TryGetProperty("runoutTimestamp", out var timestampProp))
			{
				var timestampStr = timestampProp.GetString();
				if (!string.IsNullOrEmpty(timestampStr) &&
					DateTimeOffset.TryParse(timestampStr, out var parsed))
				{
					runoutTimestamp = parsed;
				}
			}

			// Get players who received cards (not folded)
			var activePlayersInHand = gamePlayers
				.Where(gp => !gp.HasFolded)
				.OrderBy(gp => gp.SeatPosition)
				.ToList();

			// Build runout cards by seat
			var runoutCardsBySeat = new Dictionary<int, IReadOnlyList<CardPublicDto>>();

			foreach (var player in activePlayersInHand)
			{
				// Get cards dealt during the runout streets for this player
				var runoutCards = await _context.GameCards
					.Where(gc => gc.GameId == game.Id
							 && gc.GamePlayerId == player.Id
							 && gc.HandNumber == game.CurrentHandNumber
							 && gc.DealtAtPhase != null
							 && runoutStreets.Contains(gc.DealtAtPhase)
							 && !gc.IsDiscarded)
					.OrderBy(gc => gc.DealtAt)
					.ThenBy(gc => gc.DealOrder)
					.AsNoTracking()
					.ToListAsync(cancellationToken);

				if (runoutCards.Count > 0)
				{
					runoutCardsBySeat[player.SeatPosition] = runoutCards.Select(c => new CardPublicDto
					{
						IsFaceUp = c.IsVisible,
						Rank = MapSymbolToRank(c.Symbol),
						Suit = c.Suit.ToString(),
						DealOrder = c.DealOrder
					}).ToList();
				}
			}

			// Map street names to friendly descriptions
			var streetDescriptions = new Dictionary<string, string>
									{
										{ "FourthStreet", "Fourth Street" },
										{ "FifthStreet", "Fifth Street" },
										{ "SixthStreet", "Sixth Street" },
										{ "SeventhStreet", "Seventh Street (River)" }
									};

			var currentStreet = runoutStreets.LastOrDefault();
			var currentStreetDescription = currentStreet != null && streetDescriptions.TryGetValue(currentStreet, out var desc)
				? desc
				: currentStreet;

			return new AllInRunoutStateDto
			{
				IsActive = true,
				CurrentStreet = currentStreet,
				CurrentStreetDescription = currentStreetDescription,
				TotalStreets = runoutStreets.Count,
				StreetsDealt = runoutCardsBySeat.Count,
				RunoutCardsBySeat = runoutCardsBySeat,
				CurrentDealingSeatIndex = -1, // Dealing complete
				IsComplete = true
			};
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Failed to parse GameSettings JSON for game {GameId}", game.Id);
			return null;
		}
	}

	/// <summary>
	/// Builds the chip check pause state for Kings and Lows games.
	/// </summary>
	private static ChipCheckPauseStateDto? BuildChipCheckPauseState(
		Game game,
		List<GamePlayer> gamePlayers,
		int currentPot)
	{
		if (!game.IsPausedForChipCheck)
		{
			return null;
		}

		var isRebuyGracePause = game.IsPausedForRebuyGrace;
		var shortPlayers = isRebuyGracePause
			? gamePlayers
				.Where(gp => gp.Status is Entities.GamePlayerStatus.Active or Entities.GamePlayerStatus.SittingOut &&
							 gp.LeftAtHandNumber == -1 &&
							 gp.ChipStack <= 0)
				.Select(gp => new ShortPlayerDto
				{
					SeatIndex = gp.SeatPosition,
					PlayerName = gp.Player?.Name ?? $"Seat {gp.SeatPosition}",
					PlayerFirstName = gp.Player?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
					CurrentChips = gp.ChipStack,
					ChipsNeeded = Math.Max(1, (game.Ante ?? 0) - gp.ChipStack)
				})
				.ToList()
			: gamePlayers
				.Where(gp => gp.Status == Entities.GamePlayerStatus.Active &&
							 !gp.IsSittingOut &&
							 gp.ChipStack < currentPot &&
							 !gp.AutoDropOnDropOrStay)
				.Select(gp => new ShortPlayerDto
				{
					SeatIndex = gp.SeatPosition,
					PlayerName = gp.Player?.Name ?? $"Seat {gp.SeatPosition}",
					PlayerFirstName = gp.Player?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
					CurrentChips = gp.ChipStack,
					ChipsNeeded = currentPot - gp.ChipStack
				})
				.ToList();

		return new ChipCheckPauseStateDto
		{
			IsPaused = true,
			PauseStartedAt = isRebuyGracePause ? game.RebuyGraceStartedAt : game.ChipCheckPauseStartedAt,
			PauseEndsAt = isRebuyGracePause ? game.RebuyGraceEndsAt : game.ChipCheckPauseEndsAt,
			PotAmountToCover = isRebuyGracePause ? Math.Max(1, game.Ante ?? 0) : currentPot,
			ShortPlayers = shortPlayers,
			PauseType = isRebuyGracePause ? "RebuyGrace" : "ChipCoverage"
		};
	}
}
