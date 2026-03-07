using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.IrishHoldEm;

/// <summary>
/// Orchestrates an Irish Hold 'Em poker game.
/// Deals 4 hole cards like Omaha; after the flop betting round, each player must discard
/// exactly 2 of their 4 hole cards, then play continues as Texas Hold 'Em.
/// </summary>
[PokerGameMetadata(
    code: "IRISHHOLDEM",
    name: "Irish Hold 'Em",
    description: "Deal 4 hole cards, discard 2 after the flop, then play like Hold 'Em with community cards.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 4,
    initialBoardCards: 0,
    maxCommunityCards: 5,
    maxPlayerCards: 4,
    hasDrawPhase: true,
    maxDiscards: 2,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "irishholdem.png")]
public class IrishHoldEmGame : IPokerGame
{
	public string Name { get; } = "Irish Hold 'Em";
	public string Description { get; } = "Deal 4 hole cards, discard 2 after the flop, then play like Hold 'Em with community cards.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	private const int HoleCardsCount = 4;

	private readonly List<IrishHoldEmGamePlayer> _gamePlayers;
	private readonly FrenchDeckDealer _dealer;
	private readonly int _smallBlind;
	private readonly int _bigBlind;

	private PotManager _potManager;
	private BettingRound _currentBettingRound;
	private int _dealerPosition;
	private List<Card> _communityCards = new List<Card>();

	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<IrishHoldEmGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
	public int TotalPot => _potManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound => _currentBettingRound;
	public int DealerPosition => _dealerPosition;
	public PotManager PotManager => _potManager;
	public int SmallBlind => _smallBlind;
	public int BigBlind => _bigBlind;

	/// <summary>
	/// Constructor for rules discovery only.
	/// </summary>
	public IrishHoldEmGame()
		: this(
			new[] { ("P1", 100), ("P2", 100) },
			smallBlind: 0,
			bigBlind: 0)
	{
	}

	public IrishHoldEmGame(
		IEnumerable<(string name, int chips)> players,
		int smallBlind,
		int bigBlind)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
		{
			throw new ArgumentException($"Irish Hold 'Em requires at least {MinimumNumberOfPlayers} players");
		}

		if (playerList.Count > MaximumNumberOfPlayers)
		{
			throw new ArgumentException($"Irish Hold 'Em supports at most {MaximumNumberOfPlayers} players");
		}

		_gamePlayers = playerList
			.Select(p => new IrishHoldEmGamePlayer(new PokerPlayer(p.name, p.chips)))
			.ToList();

		_dealer = FrenchDeckDealer.WithFullDeck();
		_smallBlind = smallBlind;
		_bigBlind = bigBlind;
		_dealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	/// <summary>
	/// Gets the game rules metadata for Irish Hold 'Em.
	/// </summary>
	public GameFlow.GameRules GetGameRules()
	{
		return IrishHoldEmRules.CreateGameRules();
	}

	/// <summary>
	/// Starts a new hand.
	/// </summary>
	public void StartHand()
	{
		_dealer.Shuffle();
		_potManager = new PotManager();
		_communityCards.Clear();

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		CurrentPhase = Phases.CollectingBlinds;
	}

	/// <summary>
	/// Gets the small blind player position (left of dealer).
	/// </summary>
	public int SmallBlindPosition => (_dealerPosition + 1) % _gamePlayers.Count;

	/// <summary>
	/// Gets the big blind player position (left of small blind).
	/// </summary>
	public int BigBlindPosition => (_dealerPosition + 2) % _gamePlayers.Count;

	/// <summary>
	/// Collects blinds from players.
	/// </summary>
	public List<BettingAction> PostBlinds()
	{
		if (CurrentPhase != Phases.CollectingBlinds)
		{
			throw new InvalidOperationException("Cannot post blinds in current phase");
		}

		var actions = new List<BettingAction>();

		// Small blind
		var sbPlayer = _gamePlayers[SmallBlindPosition];
		var sbAmount = Math.Min(_smallBlind, sbPlayer.Player.ChipStack);
		if (sbAmount > 0)
		{
			var actualSb = sbPlayer.Player.PlaceBet(sbAmount);
			_potManager.AddContribution(sbPlayer.Player.Name, actualSb);
			actions.Add(new BettingAction(sbPlayer.Player.Name, BettingActionType.Post, actualSb));
		}

		// Big blind
		var bbPlayer = _gamePlayers[BigBlindPosition];
		var bbAmount = Math.Min(_bigBlind, bbPlayer.Player.ChipStack);
		if (bbAmount > 0)
		{
			var actualBb = bbPlayer.Player.PlaceBet(bbAmount);
			_potManager.AddContribution(bbPlayer.Player.Name, actualBb);
			actions.Add(new BettingAction(bbPlayer.Player.Name, BettingActionType.Post, actualBb));
		}

		CurrentPhase = Phases.PreFlop;
		return actions;
	}

	/// <summary>
	/// Deals 4 hole cards to each player.
	/// </summary>
	public void DealHoleCards()
	{
		if (CurrentPhase != Phases.PreFlop)
		{
			throw new InvalidOperationException("Cannot deal hole cards in current phase");
		}

		// Deal cards one at a time, rotating around the table
		for (int cardNum = 0; cardNum < HoleCardsCount; cardNum++)
		{
			for (int i = 0; i < _gamePlayers.Count; i++)
			{
				var playerIndex = (SmallBlindPosition + i) % _gamePlayers.Count;
				var gamePlayer = _gamePlayers[playerIndex];
				if (!gamePlayer.Player.HasFolded)
				{
					gamePlayer.AddHoleCard(_dealer.DealCard());
				}
			}
		}
	}

	/// <summary>
	/// Starts the betting round for the current phase.
	/// </summary>
	public void StartBettingRound()
	{
		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();

		// Reset current bets for the round (except preflop where blinds are already posted)
		if (CurrentPhase != Phases.PreFlop)
		{
			foreach (var gamePlayer in _gamePlayers)
			{
				gamePlayer.Player.ResetCurrentBet();
			}
		}

		int startPosition;
		int minBet;
		int initialBet = 0;
		int forcedBetPlayerIndex = -1;

		switch (CurrentPhase)
		{
			case Phases.PreFlop:
				startPosition = GetDealerPositionForFirstActingPlayer((BigBlindPosition + 1) % _gamePlayers.Count);
				minBet = _bigBlind;
				initialBet = _bigBlind;
				forcedBetPlayerIndex = BigBlindPosition;
				break;
			case Phases.Flop:
			case Phases.Turn:
			case Phases.River:
				startPosition = _dealerPosition;
				minBet = _bigBlind;
				break;
			default:
				throw new InvalidOperationException("Cannot start betting round in current phase");
		}

		_currentBettingRound = new BettingRound(activePlayers, _potManager, startPosition, minBet, initialBet, forcedBetPlayerIndex);
	}

	private int GetDealerPositionForFirstActingPlayer(int firstActingPlayerIndex)
	{
		return (firstActingPlayerIndex - 1 + _gamePlayers.Count) % _gamePlayers.Count;
	}

	/// <summary>
	/// Gets the available actions for the current player in a betting round.
	/// </summary>
	public AvailableActions GetAvailableActions()
	{
		return _currentBettingRound?.GetAvailableActions();
	}

	/// <summary>
	/// Gets the current player who needs to act.
	/// </summary>
	public PokerPlayer GetCurrentPlayer()
	{
		return _currentBettingRound?.CurrentPlayer;
	}

	/// <summary>
	/// Processes a betting action from the current player.
	/// </summary>
	public BettingRoundResult ProcessBettingAction(BettingActionType actionType, int amount = 0)
	{
		if (_currentBettingRound == null)
		{
			return new BettingRoundResult
			{
				Success = false,
				ErrorMessage = "No active betting round"
			};
		}

		var result = _currentBettingRound.ProcessAction(actionType, amount);

		if (result.RoundComplete)
		{
			AdvanceToNextPhase();
		}

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
		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}

		switch (CurrentPhase)
		{
			case Phases.PreFlop:
				CurrentPhase = Phases.Flop;
				break;
			case Phases.Flop:
				// Irish Hold 'Em: after flop betting, enter discard phase
				CurrentPhase = Phases.DrawPhase;
				break;
			case Phases.DrawPhase:
				CurrentPhase = Phases.Turn;
				break;
			case Phases.Turn:
				CurrentPhase = Phases.River;
				break;
			case Phases.River:
				_potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
				CurrentPhase = Phases.Showdown;
				break;
		}
	}

	/// <summary>
	/// Discards exactly 2 cards from a player's hand during the discard phase.
	/// </summary>
	/// <param name="playerName">The name of the player discarding.</param>
	/// <param name="discardIndices">Exactly 2 zero-based indices of cards to discard.</param>
	public void DiscardCards(string playerName, List<int> discardIndices)
	{
		if (CurrentPhase != Phases.DrawPhase)
		{
			throw new InvalidOperationException("Can only discard during the draw phase.");
		}

		var gamePlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == playerName);
		if (gamePlayer == null)
		{
			throw new ArgumentException($"Player '{playerName}' not found.");
		}

		if (gamePlayer.Player.HasFolded)
		{
			throw new InvalidOperationException("Folded players cannot discard.");
		}

		gamePlayer.DiscardCards(discardIndices);

		// Auto-advance to Turn when all active players have discarded
		if (IsDiscardingComplete())
		{
			AdvanceToNextPhase();
		}
	}

	/// <summary>
	/// Checks whether all active (non-folded) players have completed their discards.
	/// </summary>
	public bool IsDiscardingComplete()
	{
		return _gamePlayers
			.Where(gp => !gp.Player.HasFolded)
			.All(gp => gp.HasDiscarded);
	}

	/// <summary>
	/// Deals the flop (3 community cards).
	/// </summary>
	public void DealFlop()
	{
		if (CurrentPhase != Phases.Flop)
		{
			throw new InvalidOperationException("Cannot deal flop in current phase");
		}

		_communityCards.Add(_dealer.DealCard());
		_communityCards.Add(_dealer.DealCard());
		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	/// Deals the turn (4th community card).
	/// </summary>
	public void DealTurn()
	{
		if (CurrentPhase != Phases.Turn)
		{
			throw new InvalidOperationException("Cannot deal turn in current phase");
		}

		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	/// Deals the river (5th community card).
	/// </summary>
	public void DealRiver()
	{
		if (CurrentPhase != Phases.River)
		{
			throw new InvalidOperationException("Cannot deal river in current phase");
		}

		_communityCards.Add(_dealer.DealCard());
	}

	/// <summary>
	/// Performs the showdown and determines winners.
	/// Post-discard, players have 2 hole cards — evaluated as Hold 'Em hands.
	/// </summary>
	public IrishHoldEmShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
		{
			return new IrishHoldEmShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};
		}

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		// If only one player remains, they win by default
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = _potManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new IrishHoldEmShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands = new Dictionary<string, (HoldemHand hand, IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)>
				{
					{ winner.Player.Name, (null, winner.HoleCards.ToList(), _communityCards.ToList()) }
				},
				WonByFold = true
			};
		}

		// Evaluate hands using HoldemHand (players should have 2 hole cards post-discard)
		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp =>
			{
				var hand = new HoldemHand(gp.HoleCards.ToList(), _communityCards.ToList());
				return (hand, holeCards: (IReadOnlyCollection<Card>)gp.HoleCards.ToList(), communityCards: (IReadOnlyCollection<Card>)_communityCards.ToList());
			}
		);

		// Award pots
		var payouts = _potManager.AwardPots(eligiblePlayers =>
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

		return new IrishHoldEmShowdownResult
		{
			Success = true,
			Payouts = payouts,
			PlayerHands = playerHands
		};
	}

	private void MoveDealer()
	{
		_dealerPosition = (_dealerPosition + 1) % _gamePlayers.Count;
	}

	/// <summary>
	/// Gets the players who can continue playing (have chips).
	/// </summary>
	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	/// <summary>
	/// Checks if the game can continue (at least 2 players have chips).
	/// </summary>
	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

	/// <summary>
	/// Gets the current minimum bet for this street.
	/// </summary>
	public int GetCurrentMinBet()
	{
		return _bigBlind;
	}

	/// <summary>
	/// Gets the street name for display purposes.
	/// </summary>
	public string GetCurrentStreetName()
	{
		return CurrentPhase switch
		{
			Phases.PreFlop => "Preflop",
			Phases.Flop => "Flop",
			Phases.DrawPhase => "Discard",
			Phases.Turn => "Turn",
			Phases.River => "River",
			_ => CurrentPhase.ToString()
		};
	}

	/// <summary>
	/// Gets the dealer player.
	/// </summary>
	public IrishHoldEmGamePlayer GetDealer()
	{
		return _gamePlayers[_dealerPosition];
	}

	/// <summary>
	/// Gets the small blind player.
	/// </summary>
	public IrishHoldEmGamePlayer GetSmallBlindPlayer()
	{
		return _gamePlayers[SmallBlindPosition];
	}

	/// <summary>
	/// Gets the big blind player.
	/// </summary>
	public IrishHoldEmGamePlayer GetBigBlindPlayer()
	{
		return _gamePlayers[BigBlindPosition];
	}
}
