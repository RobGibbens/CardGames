using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Games.GoodBadUgly;

[PokerGameMetadata(
	"GOODBADUGLY",
	"The Good, the Bad, and the Ugly",
	"4 hole cards plus 3 community cards. The Good sets wilds, The Bad discards matching cards, and The Ugly creates dead hands that can still bet the final round.",
	2,
	10,
	4,
	0,
	3,
	4,
	false,
	0,
	WildCardRule.Dynamic,
	BettingStructure.Ante,
	"goodbadugly.png")]
public class GoodBadUglyGame : IPokerGame
{
	private readonly FrenchDeckDealer _dealer;
	private readonly List<string> _eliminatedPlayers = new();

	private readonly List<GoodBadUglyGamePlayer> _gamePlayers;

	private readonly List<Card> _tableCards = new();

	public GoodBadUglyGame()
		: this(new[] { ("P1", 100), ("P2", 100) }, 0, 0, 0, 0)
	{
	}

	public GoodBadUglyGame(
		IEnumerable<(string name, int chips)> players,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet,
		bool useBringIn = false)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
			throw new ArgumentException(
				$"The Good, the Bad, and the Ugly requires at least {MinimumNumberOfPlayers} players");

		if (playerList.Count > MaximumNumberOfPlayers)
			throw new ArgumentException(
				$"The Good, the Bad, and the Ugly supports at most {MaximumNumberOfPlayers} players");

		_gamePlayers = playerList
			.Select(p => new GoodBadUglyGamePlayer(new PokerPlayer(p.name, p.chips)))
			.ToList();

		_dealer = FrenchDeckDealer.WithFullDeck();
		Ante = ante;
		SmallBet = smallBet;
		BigBet = bigBet;
		DealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<GoodBadUglyGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public int TotalPot => PotManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound { get; private set; }

	public int DealerPosition { get; private set; }

	public PotManager PotManager { get; private set; }

	public int Ante { get; }

	public int BringIn => 0;
	public int SmallBet { get; }

	public int BigBet { get; }

	public bool UseBringIn => false;
	public IReadOnlyList<Card> TableCards => _tableCards.AsReadOnly();
	public int? WildRank { get; private set; }

	public int? DiscardRank { get; private set; }

	public int? EliminationRank { get; private set; }

	public IReadOnlyList<string> EliminatedPlayers => _eliminatedPlayers.AsReadOnly();
	public string Name { get; } = "The Good, the Bad, and the Ugly";

	public string Description { get; } =
		"4 hole cards plus 3 community cards. The Good sets wilds, The Bad discards matching cards, and The Ugly creates dead hands that can still bet the final round.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return GoodBadUglyRules.CreateGameRules();
	}

	public void StartHand()
	{
		_dealer.Shuffle();
		PotManager = new PotManager();
		_tableCards.Clear();
		WildRank = null;
		DiscardRank = null;
		EliminationRank = null;
		_eliminatedPlayers.Clear();

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		_tableCards.Add(_dealer.DealCard());
		_tableCards.Add(_dealer.DealCard());
		_tableCards.Add(_dealer.DealCard());

		CurrentPhase = Phases.CollectingAntes;
	}

	public List<BettingAction> CollectAntes()
	{
		if (CurrentPhase != Phases.CollectingAntes)
			throw new InvalidOperationException("Cannot collect antes in current phase");

		var actions = new List<BettingAction>();

		foreach (var gamePlayer in _gamePlayers)
		{
			var player = gamePlayer.Player;
			var anteAmount = Math.Min(Ante, player.ChipStack);

			if (anteAmount > 0)
			{
				var actualAmount = player.PlaceBet(anteAmount);
				PotManager.AddContribution(player.Name, actualAmount);
				actions.Add(new BettingAction(player.Name, BettingActionType.Post, actualAmount));
			}
		}

		CurrentPhase = Phases.ThirdStreet;
		return actions;
	}

	public void DealThirdStreet()
	{
		if (CurrentPhase != Phases.ThirdStreet)
			throw new InvalidOperationException("Cannot deal third street in current phase");

		foreach (var gamePlayer in _gamePlayers)
			if (!gamePlayer.Player.HasFolded)
				for (var i = 0; i < 4; i++)
					gamePlayer.AddHoleCard(_dealer.DealCard());

		foreach (var gamePlayer in _gamePlayers) gamePlayer.Player.ResetCurrentBet();
	}

	public BettingAction PostBringIn()
	{
		throw new InvalidOperationException("Bring-in is not used in The Good, the Bad, and the Ugly.");
	}

	public void StartBettingRound()
	{
		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		var startPosition = FindFirstActivePlayerAfterDealer();
		var minBet = CurrentPhase is Phases.ThirdStreet or Phases.FourthStreet ? SmallBet : BigBet;

		CurrentBettingRound = new BettingRound(activePlayers, PotManager, startPosition, minBet, 0,
			-1);
	}

	public AvailableActions GetAvailableActions()
	{
		return CurrentBettingRound?.GetAvailableActions();
	}

	public PokerPlayer GetCurrentPlayer()
	{
		return CurrentBettingRound?.CurrentPlayer;
	}

	public GoodBadUglyGamePlayer GetBringInPlayer()
	{
		return null;
	}

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
		var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
		if (playersInHand <= 1)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		var hasAllIn = _gamePlayers.Any(gp => gp.Player.IsAllIn && !gp.Player.HasFolded);
		if (hasAllIn) PotManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));

		var activePlayersWhoCanAct = _gamePlayers.Count(gp => !gp.Player.HasFolded && !gp.Player.IsAllIn);
		if (activePlayersWhoCanAct == 0)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		foreach (var gamePlayer in _gamePlayers) gamePlayer.Player.ResetCurrentBet();

		CurrentPhase = CurrentPhase switch
		{
			Phases.ThirdStreet => Phases.RevealTheGood,
			Phases.FourthStreet => Phases.RevealTheBad,
			Phases.FifthStreet => Phases.RevealTheUgly,
			Phases.SixthStreet => Phases.Showdown,
			_ => CurrentPhase
		};
	}

	public void DealStreetCard()
	{
		throw new InvalidOperationException("No additional street cards are dealt in this game variant.");
	}

	public Card RevealTheGood()
	{
		if (CurrentPhase != Phases.RevealTheGood)
			throw new InvalidOperationException("Cannot reveal The Good in current phase");

		var goodCard = _tableCards[0];
		WildRank = goodCard.Value;
		CurrentPhase = Phases.FourthStreet;
		return goodCard;
	}

	public (Card badCard, Dictionary<string, List<Card>> discards) RevealTheBad()
	{
		if (CurrentPhase != Phases.RevealTheBad)
			throw new InvalidOperationException("Cannot reveal The Bad in current phase");

		var badCard = _tableCards[1];
		DiscardRank = badCard.Value;

		var discards = new Dictionary<string, List<Card>>();

		foreach (var gamePlayer in _gamePlayers)
			if (!gamePlayer.Player.HasFolded)
			{
				var removed = gamePlayer.RemoveMatchingCards(badCard.Value);
				if (removed.Count > 0) discards[gamePlayer.Player.Name] = removed;
			}

		CurrentPhase = Phases.FifthStreet;
		return (badCard, discards);
	}

	public (Card uglyCard, List<string> eliminatedPlayers) RevealTheUgly()
	{
		if (CurrentPhase != Phases.RevealTheUgly)
			throw new InvalidOperationException("Cannot reveal The Ugly in current phase");

		var uglyCard = _tableCards[2];
		EliminationRank = uglyCard.Value;

		var eliminated = new List<string>();

		foreach (var gamePlayer in _gamePlayers)
			if (!gamePlayer.Player.HasFolded && gamePlayer.HasMatchingCard(uglyCard.Value))
			{
				gamePlayer.EliminateByUgly();
				eliminated.Add(gamePlayer.Player.Name);
			}

		_eliminatedPlayers.AddRange(eliminated);
		CurrentPhase = Phases.SixthStreet;

		return (uglyCard, eliminated);
	}

	public GoodBadUglyShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
			return new GoodBadUglyShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = PotManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new GoodBadUglyShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands = new Dictionary<string, (StudHand hand, IReadOnlyCollection<Card> cards)>
				{
					{ winner.Player.Name, (null!, winner.HoleCards.Concat(_tableCards).ToList()) }
				},
				WonByFold = true,
				TableCards = _tableCards.AsReadOnly(),
				WildRank = WildRank,
				DiscardRank = DiscardRank,
				EliminationRank = EliminationRank,
				EliminatedPlayers = _eliminatedPlayers.AsReadOnly(),
				AllRemainingPlayersEliminatedByUgly = winner.IsEliminatedByUgly
			};
		}

		var wildCardRules = new GoodBadUglyWildCardRules();
		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp =>
			{
				var handCards = gp.HoleCards.Concat(_tableCards).ToList();
				var hand = new GoodBadUglyHand(handCards, [], [], WildRank, wildCardRules);
				return (hand: (StudHand)hand, cards: (IReadOnlyCollection<Card>)handCards);
			});

		var eligiblePlayers = playersInHand.Where(gp => !gp.IsEliminatedByUgly).ToList();
		var allRemainingEliminated = eligiblePlayers.Count == 0;
		Dictionary<string, int> payouts;

		if (allRemainingEliminated)
		{
			var totalPot = PotManager.TotalPotAmount;
			var splitAmount = totalPot / playersInHand.Count;
			var remainder = totalPot % playersInHand.Count;

			payouts = playersInHand.ToDictionary(gp => gp.Player.Name, _ => splitAmount);
			if (remainder > 0) payouts[playersInHand[0].Player.Name] += remainder;
		}
		else
		{
			payouts = PotManager.AwardPots(eligible =>
			{
				var eligibleHands = playerHands
					.Where(kvp => eligible.Contains(kvp.Key) && eligiblePlayers.Any(gp => gp.Player.Name == kvp.Key))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.hand);

				var maxStrength = eligibleHands.Values.Max(h => h.Strength);
				return eligibleHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key);
			});
		}

		foreach (var payout in payouts)
		{
			var gamePlayer = _gamePlayers.First(gp => gp.Player.Name == payout.Key);
			gamePlayer.Player.AddChips(payout.Value);
		}

		CurrentPhase = Phases.Complete;
		MoveDealer();

		return new GoodBadUglyShowdownResult
		{
			Success = true,
			Payouts = payouts,
			PlayerHands = playerHands,
			TableCards = _tableCards.AsReadOnly(),
			WildRank = WildRank,
			DiscardRank = DiscardRank,
			EliminationRank = EliminationRank,
			EliminatedPlayers = _eliminatedPlayers.AsReadOnly(),
			AllRemainingPlayersEliminatedByUgly = allRemainingEliminated
		};
	}

	private int FindFirstActivePlayerAfterDealer()
	{
		var playerCount = _gamePlayers.Count;
		for (var offset = 1; offset <= playerCount; offset++)
		{
			var index = (DealerPosition + offset) % playerCount;
			var player = _gamePlayers[index].Player;
			if (!player.HasFolded && !player.IsAllIn) return index;
		}

		return (DealerPosition + 1) % playerCount;
	}

	private void MoveDealer()
	{
		DealerPosition = (DealerPosition + 1) % _gamePlayers.Count;
	}

	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

	public int GetCurrentMinBet()
	{
		return CurrentPhase switch
		{
			Phases.ThirdStreet => SmallBet,
			Phases.FourthStreet => SmallBet,
			Phases.FifthStreet => BigBet,
			Phases.SixthStreet => BigBet,
			_ => SmallBet
		};
	}

	public string GetCurrentStreetName()
	{
		return CurrentPhase switch
		{
			Phases.ThirdStreet => "Initial Betting",
			Phases.RevealTheGood => "Reveal The Good",
			Phases.FourthStreet => "Betting After The Good",
			Phases.RevealTheBad => "Reveal The Bad",
			Phases.FifthStreet => "Betting After The Bad",
			Phases.RevealTheUgly => "Reveal The Ugly",
			Phases.SixthStreet => "Final Betting",
			_ => CurrentPhase.ToString()
		};
	}
}