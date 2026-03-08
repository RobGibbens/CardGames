using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Games.GoodBadUgly;

[PokerGameMetadata(
    code: "GOODBADUGLY",
    name: "The Good, the Bad, and the Ugly",
    description: "4 hole cards plus 3 community cards. The Good sets wilds, The Bad discards matching cards, and The Ugly creates dead hands that can still bet the final round.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 4,
    initialBoardCards: 0,
    maxCommunityCards: 3,
    maxPlayerCards: 4,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.Dynamic,
    bettingStructure: BettingStructure.Ante,
    imageName: "goodbadugly.png")]
public class GoodBadUglyGame : IPokerGame
{
    public string Name { get; } = "The Good, the Bad, and the Ugly";
    public string Description { get; } = "4 hole cards plus 3 community cards. The Good sets wilds, The Bad discards matching cards, and The Ugly creates dead hands that can still bet the final round.";
    public CardGames.Poker.Games.VariantType VariantType { get; } = CardGames.Poker.Games.VariantType.HoldEm;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 10;

    private readonly List<GoodBadUglyGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _smallBet;
    private readonly int _bigBet;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;

    private readonly List<Card> _tableCards = new();
    private int? _wildRank;
    private int? _discardRank;
    private int? _eliminationRank;
    private readonly List<string> _eliminatedPlayers = new();

    public Phases CurrentPhase { get; private set; }
    public IReadOnlyList<GoodBadUglyGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int Ante => _ante;
    public int BringIn => 0;
    public int SmallBet => _smallBet;
    public int BigBet => _bigBet;
    public bool UseBringIn => false;
    public IReadOnlyList<Card> TableCards => _tableCards.AsReadOnly();
    public int? WildRank => _wildRank;
    public int? DiscardRank => _discardRank;
    public int? EliminationRank => _eliminationRank;
    public IReadOnlyList<string> EliminatedPlayers => _eliminatedPlayers.AsReadOnly();

    public GoodBadUglyGame()
        : this(new[] { ("P1", 100), ("P2", 100) }, ante: 0, bringIn: 0, smallBet: 0, bigBet: 0)
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
        {
            throw new ArgumentException($"The Good, the Bad, and the Ugly requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"The Good, the Bad, and the Ugly supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new GoodBadUglyGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _smallBet = smallBet;
        _bigBet = bigBet;
        _dealerPosition = 0;
        CurrentPhase = Phases.WaitingToStart;
    }

    public GameFlow.GameRules GetGameRules() => GoodBadUglyRules.CreateGameRules();

    public void StartHand()
    {
        _dealer.Shuffle();
        _potManager = new PotManager();
        _tableCards.Clear();
        _wildRank = null;
        _discardRank = null;
        _eliminationRank = null;
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
        {
            throw new InvalidOperationException("Cannot collect antes in current phase");
        }

        var actions = new List<BettingAction>();

        foreach (var gamePlayer in _gamePlayers)
        {
            var player = gamePlayer.Player;
            var anteAmount = Math.Min(_ante, player.ChipStack);

            if (anteAmount > 0)
            {
                var actualAmount = player.PlaceBet(anteAmount);
                _potManager.AddContribution(player.Name, actualAmount);
                actions.Add(new BettingAction(player.Name, BettingActionType.Post, actualAmount));
            }
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

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                for (var i = 0; i < 4; i++)
                {
                    gamePlayer.AddHoleCard(_dealer.DealCard());
                }
            }
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }
    }

    public BettingAction PostBringIn()
    {
        throw new InvalidOperationException("Bring-in is not used in The Good, the Bad, and the Ugly.");
    }

    public void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        var startPosition = FindFirstActivePlayerAfterDealer();
        var minBet = CurrentPhase is Phases.ThirdStreet or Phases.FourthStreet ? _smallBet : _bigBet;

        _currentBettingRound = new BettingRound(activePlayers, _potManager, startPosition, minBet, initialBet: 0, forcedBetPlayerIndex: -1);
    }

    public AvailableActions GetAvailableActions() => _currentBettingRound?.GetAvailableActions();

    public PokerPlayer GetCurrentPlayer() => _currentBettingRound?.CurrentPlayer;

    public GoodBadUglyGamePlayer GetBringInPlayer() => null;

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
        {
            throw new InvalidOperationException("Cannot reveal The Good in current phase");
        }

        var goodCard = _tableCards[0];
        _wildRank = goodCard.Value;
        CurrentPhase = Phases.FourthStreet;
        return goodCard;
    }

    public (Card badCard, Dictionary<string, List<Card>> discards) RevealTheBad()
    {
        if (CurrentPhase != Phases.RevealTheBad)
        {
            throw new InvalidOperationException("Cannot reveal The Bad in current phase");
        }

        var badCard = _tableCards[1];
        _discardRank = badCard.Value;

        var discards = new Dictionary<string, List<Card>>();

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                var removed = gamePlayer.RemoveMatchingCards(badCard.Value);
                if (removed.Count > 0)
                {
                    discards[gamePlayer.Player.Name] = removed;
                }
            }
        }

        CurrentPhase = Phases.FifthStreet;
        return (badCard, discards);
    }

    public (Card uglyCard, List<string> eliminatedPlayers) RevealTheUgly()
    {
        if (CurrentPhase != Phases.RevealTheUgly)
        {
            throw new InvalidOperationException("Cannot reveal The Ugly in current phase");
        }

        var uglyCard = _tableCards[2];
        _eliminationRank = uglyCard.Value;

        var eliminated = new List<string>();

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded && gamePlayer.HasMatchingCard(uglyCard.Value))
            {
                gamePlayer.EliminateByUgly();
                eliminated.Add(gamePlayer.Player.Name);
            }
        }

        _eliminatedPlayers.AddRange(eliminated);
        CurrentPhase = Phases.SixthStreet;

        return (uglyCard, eliminated);
    }

    public GoodBadUglyShowdownResult PerformShowdown()
    {
        if (CurrentPhase != Phases.Showdown)
        {
            return new GoodBadUglyShowdownResult
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
                WildRank = _wildRank,
                DiscardRank = _discardRank,
                EliminationRank = _eliminationRank,
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
                var hand = new GoodBadUglyHand(handCards, [], [], _wildRank, wildCardRules);
                return (hand: (StudHand)hand, cards: (IReadOnlyCollection<Card>)handCards);
            });

        var eligiblePlayers = playersInHand.Where(gp => !gp.IsEliminatedByUgly).ToList();
        var allRemainingEliminated = eligiblePlayers.Count == 0;
        Dictionary<string, int> payouts;

        if (allRemainingEliminated)
        {
            var totalPot = _potManager.TotalPotAmount;
            var splitAmount = totalPot / playersInHand.Count;
            var remainder = totalPot % playersInHand.Count;

            payouts = playersInHand.ToDictionary(gp => gp.Player.Name, _ => splitAmount);
            if (remainder > 0)
            {
                payouts[playersInHand[0].Player.Name] += remainder;
            }
        }
        else
        {
            payouts = _potManager.AwardPots(eligible =>
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
            WildRank = _wildRank,
            DiscardRank = _discardRank,
            EliminationRank = _eliminationRank,
            EliminatedPlayers = _eliminatedPlayers.AsReadOnly(),
            AllRemainingPlayersEliminatedByUgly = allRemainingEliminated
        };
    }

    private int FindFirstActivePlayerAfterDealer()
    {
        var playerCount = _gamePlayers.Count;
        for (var offset = 1; offset <= playerCount; offset++)
        {
            var index = (_dealerPosition + offset) % playerCount;
            var player = _gamePlayers[index].Player;
            if (!player.HasFolded && !player.IsAllIn)
            {
                return index;
            }
        }

        return (_dealerPosition + 1) % playerCount;
    }

    private void MoveDealer()
    {
        _dealerPosition = (_dealerPosition + 1) % _gamePlayers.Count;
    }

    public IEnumerable<PokerPlayer> GetPlayersWithChips() => _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);

    public bool CanContinue() => GetPlayersWithChips().Count() >= 2;

    public int GetCurrentMinBet()
    {
        return CurrentPhase switch
        {
            Phases.ThirdStreet => _smallBet,
            Phases.FourthStreet => _smallBet,
            Phases.FifthStreet => _bigBet,
            Phases.SixthStreet => _bigBet,
            _ => _smallBet
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
