using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Games.PairPressure;

/// <summary>
/// Orchestrates a Pair Pressure poker game with betting.
/// Pair Pressure is a seven card stud variant where face-up paired ranks become wild,
/// and only the two most recent paired ranks remain active.
/// </summary>
[PokerGameMetadata(
	code: "PAIRPRESSURE",
	name: "Pair Pressure",
	description: "A seven card stud poker variant where face-up paired ranks become wild, with only the two most recent paired ranks remaining active.",
	minimumNumberOfPlayers: 2,
	maximumNumberOfPlayers: 7,
	initialHoleCards: 2,
	initialBoardCards: 1,
	maxCommunityCards: 0,
	maxPlayerCards: 7,
	hasDrawPhase: false,
	maxDiscards: 0,
	wildCardRule: WildCardRule.Dynamic,
	bettingStructure: BettingStructure.AnteBringIn,
	imageName: "pairpressure.png")]
public class PairPressureGame : IPokerGame
{
	private readonly List<PairPressureGamePlayer> _gamePlayers;
	private readonly FrenchDeckDealer _dealer;
	private readonly int _ante;
	private readonly int _bringIn;
	private readonly int _smallBet;
	private readonly int _bigBet;
	private readonly bool _useBringIn;
	private readonly PairPressureWildCardRules _wildCardRules = new();

	private PotManager _potManager;
	private BettingRound _currentBettingRound;
	private int _dealerPosition;
	private int _bringInPlayerIndex;
	private List<Card> _faceUpCardsInOrder = new();

	public string Name { get; } = "Pair Pressure";
	public string Description { get; } = "A seven card stud poker variant where face-up paired ranks become wild, with only the two most recent paired ranks remaining active.";
	public CardGames.Poker.Games.VariantType VariantType { get; } = CardGames.Poker.Games.VariantType.Stud;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 7;
	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<PairPressureGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public int TotalPot => _potManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound => _currentBettingRound;
	public int DealerPosition => _dealerPosition;
	public PotManager PotManager => _potManager;
	public int Ante => _ante;
	public int BringIn => _bringIn;
	public int SmallBet => _smallBet;
	public int BigBet => _bigBet;
	public IReadOnlyList<Card> FaceUpCardsInOrder => _faceUpCardsInOrder.AsReadOnly();
	public bool UseBringIn => _useBringIn;

	/// <summary>
	/// Constructor for rules discovery only.
	/// </summary>
	public PairPressureGame()
		: this(
			[("P1", 100), ("P2", 100)],
			ante: 0,
			bringIn: 0,
			smallBet: 0,
			bigBet: 0)
	{
	}

	public PairPressureGame(
		IEnumerable<(string name, int chips)> players,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet,
		bool useBringIn = true)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
		{
			throw new ArgumentException($"Pair Pressure requires at least {MinimumNumberOfPlayers} players");
		}

		if (playerList.Count > MaximumNumberOfPlayers)
		{
			throw new ArgumentException($"Pair Pressure supports at most {MaximumNumberOfPlayers} players");
		}

		_gamePlayers = playerList
			.Select(player => new PairPressureGamePlayer(new PokerPlayer(player.name, player.chips)))
			.ToList();

		_dealer = FrenchDeckDealer.WithFullDeck();
		_ante = ante;
		_bringIn = bringIn;
		_smallBet = smallBet;
		_bigBet = bigBet;
		_useBringIn = useBringIn;
		_dealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	public GameFlow.GameRules GetGameRules()
	{
		return PairPressureRules.CreateGameRules();
	}

	public void StartHand()
	{
		_dealer.Shuffle();
		_potManager = new PotManager();
		_faceUpCardsInOrder.Clear();

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		CurrentPhase = Phases.CollectingAntes;
	}

	public List<BettingAction> CollectAntes()
	{
		if (CurrentPhase != Phases.CollectingAntes)
		{
			throw new InvalidOperationException("Cannot collect antes in current phase");
		}

		var actions = new List<BettingAction>();

		foreach (var gamePlayer in _gamePlayers)
		{
			var player = gamePlayer.Player;
			var anteAmount = Math.Min(_ante, player.ChipStack);

			if (anteAmount <= 0)
			{
				continue;
			}

			var actualAmount = player.PlaceBet(anteAmount);
			_potManager.AddContribution(player.Name, actualAmount);
			actions.Add(new BettingAction(player.Name, BettingActionType.Post, actualAmount));
		}

		CurrentPhase = Phases.ThirdStreet;
		return actions;
	}

	public void DealThirdStreet()
	{
		if (CurrentPhase != Phases.ThirdStreet)
		{
			throw new InvalidOperationException("Cannot deal third street in current phase");
		}

		var startPosition = (_dealerPosition + 1) % _gamePlayers.Count;
		for (var index = 0; index < _gamePlayers.Count; index++)
		{
			var gamePlayer = _gamePlayers[(startPosition + index) % _gamePlayers.Count];
			if (gamePlayer.Player.HasFolded)
			{
				continue;
			}

			gamePlayer.AddHoleCard(_dealer.DealCard());
			gamePlayer.AddHoleCard(_dealer.DealCard());
			var boardCard = _dealer.DealCard();
			gamePlayer.AddBoardCard(boardCard);
			_faceUpCardsInOrder.Add(boardCard);
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}

		_bringInPlayerIndex = _useBringIn ? FindBringInPlayer() : -1;
	}

	public BettingAction PostBringIn()
	{
		if (CurrentPhase != Phases.ThirdStreet)
		{
			throw new InvalidOperationException("Cannot post bring-in in current phase");
		}

		if (!_useBringIn)
		{
			throw new InvalidOperationException("Bring-in is disabled for this game");
		}

		var bringInPlayer = _gamePlayers[_bringInPlayerIndex];
		var actualAmount = bringInPlayer.Player.PlaceBet(Math.Min(_bringIn, bringInPlayer.Player.ChipStack));
		_potManager.AddContribution(bringInPlayer.Player.Name, actualAmount);

		return new BettingAction(bringInPlayer.Player.Name, BettingActionType.Post, actualAmount);
	}

	public void StartBettingRound()
	{
		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		int startPosition;
		int minBet;
		var initialBet = 0;
		var forcedBetPlayerIndex = -1;

		switch (CurrentPhase)
		{
			case Phases.ThirdStreet:
				if (_useBringIn && _bringInPlayerIndex >= 0)
				{
					startPosition = _bringInPlayerIndex;
					var bringInPlayer = _gamePlayers[_bringInPlayerIndex].Player;
					initialBet = bringInPlayer.CurrentBet;
					forcedBetPlayerIndex = _bringInPlayerIndex;
				}
				else
				{
					startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
				}
				minBet = _smallBet;
				break;
			case Phases.FourthStreet:
				startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
				minBet = _smallBet;
				break;
			case Phases.FifthStreet:
			case Phases.SixthStreet:
			case Phases.SeventhStreet:
				startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
				minBet = _bigBet;
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

	public AvailableActions GetAvailableActions()
	{
		return _currentBettingRound?.GetAvailableActions();
	}

	public PokerPlayer GetCurrentPlayer()
	{
		return _currentBettingRound?.CurrentPlayer;
	}

	public PairPressureGamePlayer GetBringInPlayer()
	{
		if (_bringInPlayerIndex < 0 || _bringInPlayerIndex >= _gamePlayers.Count)
		{
			return null;
		}

		return _gamePlayers[_bringInPlayerIndex];
	}

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
		var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
		if (playersInHand <= 1)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		var hasAllIn = _gamePlayers.Any(gp => gp.Player.IsAllIn && !gp.Player.HasFolded);
		if (hasAllIn)
		{
			_potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
		}

		var activePlayersWhoCanAct = _gamePlayers.Count(gp => !gp.Player.HasFolded && !gp.Player.IsAllIn);
		if (activePlayersWhoCanAct == 0)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}

		CurrentPhase = CurrentPhase switch
		{
			Phases.ThirdStreet => Phases.FourthStreet,
			Phases.FourthStreet => Phases.FifthStreet,
			Phases.FifthStreet => Phases.SixthStreet,
			Phases.SixthStreet => Phases.SeventhStreet,
			Phases.SeventhStreet => Phases.Showdown,
			_ => CurrentPhase
		};
	}

	public void DealStreetCard()
	{
		if (CurrentPhase is not (Phases.FourthStreet or Phases.FifthStreet or Phases.SixthStreet or Phases.SeventhStreet))
		{
			throw new InvalidOperationException("Cannot deal street card in current phase");
		}

		var startPosition = (_dealerPosition + 1) % _gamePlayers.Count;
		for (var index = 0; index < _gamePlayers.Count; index++)
		{
			var gamePlayer = _gamePlayers[(startPosition + index) % _gamePlayers.Count];
			if (gamePlayer.Player.HasFolded)
			{
				continue;
			}

			if (CurrentPhase == Phases.SeventhStreet)
			{
				gamePlayer.AddHoleCard(_dealer.DealCard());
			}
			else
			{
				var boardCard = _dealer.DealCard();
				gamePlayer.AddBoardCard(boardCard);
				_faceUpCardsInOrder.Add(boardCard);
			}
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}
	}

	public PairPressureShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
		{
			return new PairPressureShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};
		}

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = _potManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new PairPressureShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands = new Dictionary<string, (PairPressureHand hand, IReadOnlyCollection<Card> cards)>
				{
					{ winner.Player.Name, (null, winner.AllCards.ToList()) }
				},
				WonByFold = true
			};
		}

		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp =>
			{
				var holeCards = gp.HoleCards.Take(2).ToList();
				var boardCards = gp.BoardCards.ToList();
				var downCard = gp.HoleCards.Count > 2 ? gp.HoleCards[2] : holeCards.LastOrDefault();
				var allCards = gp.HoleCards.Concat(gp.BoardCards).ToList();
				var hand = new PairPressureHand(holeCards, boardCards, downCard, _faceUpCardsInOrder);
				return (hand, cards: (IReadOnlyCollection<Card>)allCards);
			});

		var payouts = _potManager.AwardPots(eligiblePlayers =>
		{
			var eligibleHands = playerHands
				.Where(kvp => eligiblePlayers.Contains(kvp.Key))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.hand);

			var maxStrength = eligibleHands.Values.Max(hand => hand.Strength);
			return eligibleHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key);
		});

		foreach (var payout in payouts)
		{
			var gamePlayer = _gamePlayers.First(gp => gp.Player.Name == payout.Key);
			gamePlayer.Player.AddChips(payout.Value);
		}

		CurrentPhase = Phases.Complete;
		MoveDealer();

		return new PairPressureShowdownResult
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

	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

	private int FindBringInPlayer()
	{
		var lowestIndex = -1;
		Card lowestCard = null;

		for (var index = 0; index < _gamePlayers.Count; index++)
		{
			var gamePlayer = _gamePlayers[index];
			if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
			{
				continue;
			}

			var upCard = gamePlayer.BoardCards[0];
			if (lowestCard is null || CompareCardsForBringIn(upCard, lowestCard) < 0)
			{
				lowestCard = upCard;
				lowestIndex = index;
			}
		}

		return lowestIndex;
	}

	private static int CompareCardsForBringIn(Card a, Card b)
	{
		if (a.Value != b.Value)
		{
			return a.Value.CompareTo(b.Value);
		}

		return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
	}

	private static int GetSuitRank(Suit suit)
	{
		return suit switch
		{
			Suit.Clubs => 0,
			Suit.Diamonds => 1,
			Suit.Hearts => 2,
			Suit.Spades => 3,
			_ => 0
		};
	}

	private int FindBestVisibleHandPosition()
	{
		var currentWildRanks = _wildCardRules.DetermineWildRanks(_faceUpCardsInOrder).ToHashSet();
		var bestIndex = -1;
		long bestStrength = -1;

		for (var index = 0; index < _gamePlayers.Count; index++)
		{
			var gamePlayer = _gamePlayers[index];
			if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
			{
				continue;
			}

			var visibleStrength = EvaluateVisibleHand(gamePlayer.BoardCards, currentWildRanks);
			if (visibleStrength > bestStrength)
			{
				bestStrength = visibleStrength;
				bestIndex = index;
			}
		}

		return bestIndex >= 0 ? bestIndex : 0;
	}

	private static long EvaluateVisibleHand(IReadOnlyCollection<Card> boardCards, HashSet<int> wildRanks)
	{
		if (boardCards.Count == 0)
		{
			return 0;
		}

		var cards = boardCards.OrderByDescending(card => card.Value).ToList();
		var valueCounts = cards
			.Where(card => !wildRanks.Contains(card.Value))
			.GroupBy(card => card.Value)
			.OrderByDescending(group => group.Count())
			.ThenByDescending(group => group.Key)
			.Select(group => new { Value = group.Key, Count = group.Count() })
			.ToList();

		var wildCount = cards.Count(card => wildRanks.Contains(card.Value));
		if (wildCount > 0)
		{
			if (valueCounts.Count == 0)
			{
				valueCounts.Add(new { Value = 14, Count = wildCount });
			}
			else
			{
				var target = valueCounts
					.OrderByDescending(value => value.Count)
					.ThenByDescending(value => value.Value)
					.First();

				valueCounts.Remove(target);
				valueCounts.Add(new { target.Value, Count = target.Count + wildCount });
				valueCounts = valueCounts
					.OrderByDescending(value => value.Count)
					.ThenByDescending(value => value.Value)
					.ToList();
			}
		}

		long strength;
		var maxCount = valueCounts.First().Count;

		if (maxCount >= 4)
		{
			strength = 7_000_000 + valueCounts.First().Value * 1000;
		}
		else if (maxCount >= 3)
		{
			strength = 4_000_000 + valueCounts.First().Value * 1000;
		}
		else if (maxCount >= 2)
		{
			var pairs = valueCounts.Where(group => group.Count >= 2).ToList();
			strength = pairs.Count >= 2
				? 3_000_000 + pairs[0].Value * 1000 + pairs[1].Value * 10
				: 2_000_000 + pairs[0].Value * 1000;
		}
		else
		{
			strength = 1_000_000;
		}

		var kickerValues = valueCounts
			.SelectMany(value => Enumerable.Repeat(value.Value, value.Count))
			.OrderByDescending(value => value)
			.Take(4)
			.ToList();

		if (kickerValues.Count == 0)
		{
			kickerValues = cards.Take(4).Select(card => card.Value).ToList();
		}

		foreach (var kickerValue in kickerValues)
		{
			strength = strength * 15 + kickerValue;
		}

		return strength;
	}

	public int GetCurrentMinBet()
	{
		return CurrentPhase switch
		{
			Phases.ThirdStreet => _smallBet,
			Phases.FourthStreet => _smallBet,
			Phases.FifthStreet => _bigBet,
			Phases.SixthStreet => _bigBet,
			Phases.SeventhStreet => _bigBet,
			_ => _smallBet
		};
	}

	public string GetCurrentStreetName()
	{
		return CurrentPhase switch
		{
			Phases.ThirdStreet => "Third Street",
			Phases.FourthStreet => "Fourth Street",
			Phases.FifthStreet => "Fifth Street",
			Phases.SixthStreet => "Sixth Street",
			Phases.SeventhStreet => "Seventh Street (River)",
			_ => CurrentPhase.ToString()
		};
	}

	public IReadOnlyList<Symbol> GetCurrentWildRanks()
	{
		return _wildCardRules
			.DetermineWildRanks(_faceUpCardsInOrder)
			.Select(value => (Symbol)value)
			.ToList();
	}
}