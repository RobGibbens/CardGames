using System.Collections.Generic;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.PairPressure;

public static class PairPressureRules
{
	public static GameRules CreateGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "PAIRPRESSURE",
			GameTypeName = "Pair Pressure",
			Description = "A seven card stud poker variant where any rank that pairs face up becomes wild, with only the two most recent paired ranks remaining active.",
			MinPlayers = 2,
			MaxPlayers = 7,
			Phases =
			[
				new()
				{
					PhaseId = "WaitingToStart",
					Name = "Waiting to Start",
					Description = "Waiting for players to join and ready up",
					Category = "Setup",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "CollectingAntes",
					Name = "Collecting Antes",
					Description = "Collecting ante bets from all players",
					Category = "Setup",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "ThirdStreet",
					Name = "Third Street",
					Description = "2 down cards and 1 up card dealt, bring-in betting round",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "FourthStreet",
					Name = "Fourth Street",
					Description = "1 up card dealt, betting round with small bet",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "FifthStreet",
					Name = "Fifth Street",
					Description = "1 up card dealt, betting round with big bet",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "SixthStreet",
					Name = "Sixth Street",
					Description = "1 up card dealt, betting round with big bet",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "SeventhStreet",
					Name = "Seventh Street (River)",
					Description = "1 down card dealt, final betting round with big bet",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "Showdown",
					Name = "Showdown",
					Description = "Players reveal hands and winner is determined",
					Category = "Resolution",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Complete",
					Name = "Complete",
					Description = "Hand is complete",
					Category = "Resolution",
					RequiresPlayerAction = false,
					IsTerminal = true
				}
			],
			CardDealing = new CardDealingConfig
			{
				InitialCards = 3,
				InitialVisibility = CardVisibility.Mixed,
				HasCommunityCards = false,
				DealingRounds =
				[
					new() { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
					new() { CardCount = 1, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players }
				]
			},
			Betting = new BettingConfig
			{
				HasAntes = true,
				HasBlinds = false,
				BettingRounds = 5,
				Structure = "Fixed Limit"
			},
			Showdown = new ShowdownConfig
			{
				HandRanking = "Pair Pressure",
				IsHighLow = false,
				HasSpecialSplitRules = false
			},
			SpecialRules = new Dictionary<string, object>
			{
				["WildCards"] = "Any face-up rank that pairs becomes wild; only the two most recent paired ranks remain active.",
				["WildCardRule"] = "Two most recent paired face-up ranks",
				["HasBringIn"] = true
			}
		};
	}
}