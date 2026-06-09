using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.Omaha;

/// <summary>
///     Orchestrates an Omaha poker game with betting.
///     Uses blinds (small blind and big blind) instead of antes.
/// </summary>
[PokerGameMetadata(
	"OMAHA",
	"Omaha",
	"A popular variant of poker where players are dealt four hole cards and share five community cards, with four betting rounds.",
	2,
	10,
	4,
	0,
	5,
	4,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Blinds,
	"omaha.png")]
public class OmahaGame : IPokerGame
{
	private const int HoleCardsCount = 4;
	private readonly List<Card> _communityCards = new();
	private readonly FrenchDeckDealer _dealer;

	private readonly List<OmahaGamePlayer> _gamePlayers;

	/// <summary>
	///     Constructor for rules discovery only.
	/// </summary>
	public OmahaGame()
		: this(
			new[] { ("P1", 100), ("P2", 100) },
			0,
			0)
	{
	}

	public OmahaGame(
		IEnumerable<(string name, int chips)> players,
		int smallBlind,
		int bigBlind)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
			throw new ArgumentException($"Omaha requires at least {MinimumNumberOfPlayers} players");

		if (playerList.Count > MaximumNumberOfPlayers)
			throw new ArgumentException($"Omaha supports at most {MaximumNumberOfPlayers} players");

		_gamePlayers =
		[
			.. playerList
				.Select(p => new OmahaGamePlayer(new PokerPlayer(p.name, p.chips)))
		];

		_dealer = FrenchDeckDealer.WithFullDeck();
		SmallBlind = smallBlind;
		BigBlind = bigBlind;
		DealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<OmahaGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
	public int TotalPot => PotManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound { get; private set; }

	public int DealerPosition { get; private set; }

	public PotManager PotManager { get; private set; }

	public int SmallBlind { get; }

	public int BigBlind { get; }

	/// <summary>
	///     Gets the small blind player position (left of dealer).
	/// </summary>
	public int SmallBlindPosition => (DealerPosition + 1) % _gamePlayers.Count;

	/// <summary>
	///     Gets the big blind player position (left of small blind).
	/// </summary>
	public int BigBlindPosition => (DealerPosition + 2) % _gamePlayers.Count;

	public string Name { get; } = "Omaha";

	public string Description { get; } =
		"A popular variant of poker where players are dealt four hole cards and share five community cards, with four betting rounds.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	/// <summary>
	///     Gets the game rules metadata for Omaha.
	/// </summary>
	public GameRules GetGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "OMAHA",
			GameTypeName = "Omaha",
			Description =
				"A popular variant of poker where players are dealt four hole cards and share five community cards, with four betting rounds.",
			MinPlayers = 2,
			MaxPlayers = 10,
			Phases = new List<GamePhaseDescriptor>
			{
				new()
				{
					PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players",
					Category = "Setup", RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "CollectingBlinds", Name = "Collecting Blinds", Description = "Collecting blinds",
					Category = "Setup", RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Dealing", Name = "Dealing", Description = "Dealing hole cards", Category = "Setup",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Showdown", Name = "Showdown", Description = "Determine winner", Category = "Resolution",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Complete", Name = "Complete", Description = "Hand complete", Category = "Resolution",
					RequiresPlayerAction = false, IsTerminal = true
				}
			},
			CardDealing = new CardDealingConfig
			{
				InitialCards = 4,
				InitialVisibility = CardVisibility.FaceDown,
				HasCommunityCards = true,
				DealingRounds = new List<DealingRound>
				{
					new() { CardCount = 4, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
					new() { CardCount = 3, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community },
					new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community }
				}
			},
			Betting = new BettingConfig
			{
				HasAntes = false,
				HasBlinds = true,
				BettingRounds = 4,
				Structure = "Pot Limit"
			},
			Showdown = new ShowdownConfig
			{
				HandRanking = "Omaha",
				IsHighLow = false,
				HasSpecialSplitRules = false
			}
		};
	}

	/// <summary>
	///     Starts a new hand.
	/// </summary>
	public void StartHand()
	{
		_dealer.Shuffle();
		PotManager = new PotManager();
		_communityCards.Clear();

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		CurrentPhase = Phases.CollectingBlinds;
	}

	/// <summary>
	///     Collects blinds from players.
	/// </summary>
	public List<BettingAction> PostBlinds()
	{
		if (CurrentPhase != Phases.CollectingBlinds)
			throw new InvalidOperationException("Cannot post blinds in current phase");

		var actions = new List<BettingAction>();

		// Small blind
		var sbPlayer = _gamePlayers[SmallBlindPosition];
		var sbAmount = Math.Min(SmallBlind, sbPlayer.Player.ChipStack);
		if (sbAmount > 0)
		{
			var actualSb = sbPlayer.Player.PlaceBet(sbAmount);
			PotManager.AddContribution(sbPlayer.Player.Name, actualSb);
			actions.Add(new BettingAction(sbPlayer.Player.Name, BettingActionType.Post, actualSb));
		}

		// Big blind
		var bbPlayer = _gamePlayers[BigBlindPosition];
		var bbAmount = Math.Min(BigBlind, bbPlayer.Player.ChipStack);
		if (bbAmount > 0)
		{
			var actualBb = bbPlayer.Player.PlaceBet(bbAmount);
			PotManager.AddContribution(bbPlayer.Player.Name, actualBb);
			actions.Add(new BettingAction(bbPlayer.Player.Name, BettingActionType.Post, actualBb));
		}

		CurrentPhase = Phases.PreFlop;
		return actions;
	}

	/// <summary>
	///     Deals 4 hole cards to each player.
	/// </summary>
	public void DealHoleCards()
	{
		if (CurrentPhase != Phases.PreFlop)
			throw new InvalidOperationException("Cannot deal hole cards in current phase");

		// Deal cards one at a time, rotating around the table
		for (var cardNum = 0; cardNum < HoleCardsCount; cardNum++)
		for (var i = 0; i < _gamePlayers.Count; i++)
		{
			var playerIndex = (SmallBlindPosition + i) % _gamePlayers.Count;
			var gamePlayer = _gamePlayers[playerIndex];
			if (!gamePlayer.Player.HasFolded) gamePlayer.AddHoleCard(_dealer.DealCard());
		}
	}

	/// <summary>
	///     Starts the betting round for the current phase.
	///     Preflop: action starts left of big blind, big blind is forced bet.
	///     Postflop: action starts left of dealer.
	/// </summary>
	public void StartBettingRound()
	{
		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();

		// Reset current bets for the round (except preflop where blinds are already posted)
		if (CurrentPhase != Phases.PreFlop)
			foreach (var gamePlayer in _gamePlayers)
				gamePlayer.Player.ResetCurrentBet();

		int startPosition;
		int minBet;
		var initialBet = 0;
		var forcedBetPlayerIndex = -1;

		switch (CurrentPhase)
		{
			case Phases.PreFlop:
				// For preflop, action starts with player left of big blind
				// Big blind is the current bet to match
				startPosition = GetDealerPositionForFirstActingPlayer((BigBlindPosition + 1) % _gamePlayers.Count);
				minBet = BigBlind;
				initialBet = BigBlind;
				forcedBetPlayerIndex = BigBlindPosition;
				break;
			case Phases.Flop:
			case Phases.Turn:
			case Phases.River:
				// For postflop, action starts with first active player left of dealer
				startPosition = DealerPosition;
				minBet = BigBlind;
				break;
			default:
				throw new InvalidOperationException("Cannot start betting round in current phase");
		}

		CurrentBettingRound = new BettingRound(activePlayers, PotManager, startPosition, minBet, initialBet,
			forcedBetPlayerIndex);
	}

	/// <summary>
	///     Converts a first-acting player index to a dealer position.
	///     BettingRound calculates the first player as (dealerPosition + 1) % playerCount,
	///     so we need to subtract 1 (with wraparound) to make the first-acting player start.
	/// </summary>
	private int GetDealerPositionForFirstActingPlayer(int firstActingPlayerIndex)
	{
		return (firstActingPlayerIndex - 1 + _gamePlayers.Count) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the available actions for the current player in a betting round.
	/// </summary>
	public AvailableActions GetAvailableActions()
	{
		return CurrentBettingRound?.GetAvailableActions();
	}

	/// <summary>
	///     Gets the current player who needs to act.
	/// </summary>
	public PokerPlayer GetCurrentPlayer()
	{
		return CurrentBettingRound?.CurrentPlayer;
	}

	/// <summary>
	///     Processes a betting action from the current player.
	/// </summary>
	public BettingRoundResult ProcessBettingAction(BettingActionType actionType, int amount = 0)
	{
		if (CurrentBettingRound == null)
			return new BettingRoundResult
			{
				Success = false,
				ErrorMessage = "No active betting round"
			};

		var result = CurrentBettingRound.ProcessAction(actionType, amount);

		if (result.RoundComplete) AdvanceToNextPhase();

		return result;
	}

	private void AdvanceToNextPhase()
	{
		// Check if only one player remains
		var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
		if (playersInHand <= 1)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		// Reset current bets for next round
		foreach (var gamePlayer in _gamePlayers) gamePlayer.Player.ResetCurrentBet();

		switch (CurrentPhase)
		{
			case Phases.PreFlop:
				CurrentPhase = Phases.Flop;
				break;
			case Phases.Flop:
				CurrentPhase = Phases.Turn;
				break;
			case Phases.Turn:
				CurrentPhase = Phases.River;
				break;
			case Phases.River:
				PotManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
				CurrentPhase = Phases.Showdown;
				break;
		}
	}

	/// <summary>
	///     Deals the flop (3 community cards).
	/// </summary>
	public void DealFlop()
	{
		if (CurrentPhase != Phases.Flop) throw new InvalidOperationException("Cannot deal flop in current phase");

		_communityCards.Add(_dealer.DealCard());
		_communityCards.Add(_dealer.DealCard());
		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	///     Deals the turn (4th community card).
	/// </summary>
	public void DealTurn()
	{
		if (CurrentPhase != Phases.Turn) throw new InvalidOperationException("Cannot deal turn in current phase");

		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	///     Deals the river (5th community card).
	/// </summary>
	public void DealRiver()
	{
		if (CurrentPhase != Phases.River) throw new InvalidOperationException("Cannot deal river in current phase");

		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	///     Performs the showdown and determines winners.
	/// </summary>
	public OmahaShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
			return new OmahaShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		// If only one player remains, they win by default
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = PotManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new OmahaShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands =
					new Dictionary<string, (OmahaHand hand, IReadOnlyCollection<Card> holeCards,
						IReadOnlyCollection<Card> communityCards)>
					{
						{ winner.Player.Name, (null, winner.HoleCards.ToList(), _communityCards.ToList()) }
					},
				WonByFold = true
			};
		}

		// Evaluate hands
		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp =>
			{
				var hand = new OmahaHand(gp.HoleCards.ToList(), _communityCards.ToList());
				return (hand, holeCards: (IReadOnlyCollection<Card>)gp.HoleCards.ToList(),
					communityCards: (IReadOnlyCollection<Card>)_communityCards.ToList());
			}
		);

		// Award pots
		var payouts = PotManager.AwardPots(eligiblePlayers =>
		{
			var eligibleHands = playerHands
				.Where(kvp => eligiblePlayers.Contains(kvp.Key))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.hand);

			var maxStrength = eligibleHands.Values.Max(h => h.Strength);
			return eligibleHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key);
		});

		// Add winnings to player stacks
		foreach (var payout in payouts)
		{
			var gamePlayer = _gamePlayers.First(gp => gp.Player.Name == payout.Key);
			gamePlayer.Player.AddChips(payout.Value);
		}

		CurrentPhase = Phases.Complete;
		MoveDealer();

		return new OmahaShowdownResult
		{
			Success = true,
			Payouts = payouts,
			PlayerHands = playerHands
		};
	}

	private void MoveDealer()
	{
		DealerPosition = (DealerPosition + 1) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the players who can continue playing (have chips).
	/// </summary>
	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	/// <summary>
	///     Checks if the game can continue (at least 2 players have chips).
	/// </summary>
	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

	/// <summary>
	///     Gets the current minimum bet for this street.
	/// </summary>
	public int GetCurrentMinBet()
	{
		return BigBlind;
	}

	/// <summary>
	///     Gets the street name for display purposes.
	/// </summary>
	public string GetCurrentStreetName()
	{
		return CurrentPhase switch
		{
			Phases.PreFlop => "Preflop",
			Phases.Flop => "Flop",
			Phases.Turn => "Turn",
			Phases.River => "River",
			_ => CurrentPhase.ToString()
		};
	}

	/// <summary>
	///     Gets the dealer player.
	/// </summary>
	public OmahaGamePlayer GetDealer()
	{
		return _gamePlayers[DealerPosition];
	}

	/// <summary>
	///     Gets the small blind player.
	/// </summary>
	public OmahaGamePlayer GetSmallBlindPlayer()
	{
		return _gamePlayers[SmallBlindPosition];
	}

	/// <summary>
	///     Gets the big blind player.
	/// </summary>
	public OmahaGamePlayer GetBigBlindPlayer()
	{
		return _gamePlayers[BigBlindPosition];
	}
}