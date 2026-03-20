using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.ScrewYourNeighbor;

public static class ScrewYourNeighborRules
{
	public static GameRules CreateGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "SCREWYOURNEIGHBOR",
			GameTypeName = "Screw Your Neighbor",
			Description = "Each player is dealt one card. Players decide to keep their card or trade with the player to their left. Kings are blockers. Lowest card loses a stack. Last player standing wins.",
			MinPlayers = 3,
			MaxPlayers = 10,
			Phases = new List<GamePhaseDescriptor>
			{
				new()
				{
					PhaseId = "WaitingToStart", Name = "Waiting to Start",
					Description = "Waiting for players", Category = "Setup",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Dealing", Name = "Dealing",
					Description = "Dealing one card to each player", Category = "Setup",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "KeepOrTrade", Name = "Keep or Trade",
					Description = "Each player decides to keep their card or trade with the next player. Kings are blockers.",
					Category = "Decision", RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Reveal", Name = "Reveal",
					Description = "All cards are revealed face up", Category = "Special",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Showdown", Name = "Showdown",
					Description = "Player(s) with the lowest card lose a stack", Category = "Resolution",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Complete", Name = "Complete",
					Description = "Round complete", Category = "Resolution",
					RequiresPlayerAction = false, IsTerminal = true
				}
			},
			CardDealing = new CardDealingConfig
			{
				InitialCards = 1,
				InitialVisibility = CardVisibility.FaceDown,
				HasCommunityCards = false,
				DealingRounds = new List<DealingRound>
				{
					new()
					{
						CardCount = 1, Visibility = CardVisibility.FaceDown,
						Target = DealingTarget.Players
					}
				}
			},
			Betting = new BettingConfig
			{
				HasAntes = true,
				HasBlinds = false,
				BettingRounds = 0,
				Structure = "Stack Elimination"
			},
			Drawing = new DrawingConfig
			{
				AllowsDrawing = false,
				MaxDiscards = 0,
				SpecialRules = "Players trade cards with neighbor instead of drawing from deck. Kings block trades."
			},
			Showdown = new ShowdownConfig
			{
				HandRanking = "SingleCardLow",
				IsHighLow = false,
				HasSpecialSplitRules = false
			},
			SpecialRules = new Dictionary<string, object>
			{
				["KeepOrTrade"] = true,
				["PotCarryOver"] = true
			}
		};
	}
}
