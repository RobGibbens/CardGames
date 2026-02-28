using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Games.GoodBadUgly;

/// <summary>
/// Orchestrates "The Good, the Bad, and the Ugly" poker game.
/// A Seven Card Stud variant with three face-down table cards that are revealed
/// between streets with special effects:
/// - The Good: matching ranks become wild
/// - The Bad: matching cards must be discarded
/// - The Ugly: players with matching face-up cards are eliminated
/// </summary>
[PokerGameMetadata(
    code: "GOODBADUGLY",
    name: "The Good, the Bad, and the Ugly",
    description: "Seven Card Stud with three table cards. 'The Good' makes a rank wild, 'The Bad' forces discards, and 'The Ugly' eliminates players with matching face-up cards.",
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
    imageName: "goodbadugly.png")]
public class GoodBadUglyGame : IPokerGame
{
    public string Name { get; } = "The Good, the Bad, and the Ugly";
    public string Description { get; } = "Seven Card Stud with three table cards. 'The Good' makes a rank wild, 'The Bad' forces discards, and 'The Ugly' eliminates players with matching face-up cards.";
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 7;

    private readonly List<GoodBadUglyGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _bringIn;
    private readonly int _smallBet;
    private readonly int _bigBet;
    private readonly bool _useBringIn;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private int _bringInPlayerIndex;

    /// <summary>
    /// The three table cards dealt face-down at the start of the hand.
    /// Index 0 = The Good, 1 = The Bad, 2 = The Ugly.
    /// </summary>
    private readonly List<Card> _tableCards = new();

    /// <summary>
    /// The rank (value) of "The Good" card once revealed. Null before reveal.
    /// </summary>
    private int? _wildRank;

    /// <summary>
    /// The rank (value) of "The Bad" card once revealed. Null before reveal.
    /// </summary>
    private int? _discardRank;

    /// <summary>
    /// The rank (value) of "The Ugly" card once revealed. Null before reveal.
    /// </summary>
    private int? _eliminationRank;

    /// <summary>
    /// Players eliminated by "The Ugly" card.
    /// </summary>
    private readonly List<string> _eliminatedPlayers = new();

    public Phases CurrentPhase { get; private set; }
    public IReadOnlyList<GoodBadUglyGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int Ante => _ante;
    public int BringIn => _bringIn;
    public int SmallBet => _smallBet;
    public int BigBet => _bigBet;
    public bool UseBringIn => _useBringIn;
    public IReadOnlyList<Card> TableCards => _tableCards.AsReadOnly();
    public int? WildRank => _wildRank;
    public int? DiscardRank => _discardRank;
    public int? EliminationRank => _eliminationRank;
    public IReadOnlyList<string> EliminatedPlayers => _eliminatedPlayers.AsReadOnly();

    /// <summary>
    /// Constructor for rules discovery only.
    /// </summary>
    public GoodBadUglyGame()
        : this(
            new[] { ("P1", 100), ("P2", 100) },
            ante: 0,
            bringIn: 0,
            smallBet: 0,
            bigBet: 0)
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
        _bringIn = bringIn;
        _smallBet = smallBet;
        _bigBet = bigBet;
        _useBringIn = useBringIn;
        _dealerPosition = 0;
        CurrentPhase = Phases.WaitingToStart;
    }

    /// <summary>
    /// Gets the game rules metadata.
    /// </summary>
    public GameFlow.GameRules GetGameRules()
    {
        return GoodBadUglyRules.CreateGameRules();
    }

    /// <summary>
    /// Starts a new hand. Shuffles the deck, deals 3 face-down table cards, and resets state.
    /// </summary>
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

        // Deal 3 table cards face-down
        _tableCards.Add(_dealer.DealCard());
        _tableCards.Add(_dealer.DealCard());
        _tableCards.Add(_dealer.DealCard());

        CurrentPhase = Phases.CollectingAntes;
    }

    /// <summary>
    /// Collects antes from all players.
    /// </summary>
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

    /// <summary>
    /// Deals the third street cards (2 hole cards + 1 board card) to all players.
    /// </summary>
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
                gamePlayer.AddHoleCard(_dealer.DealCard());
                gamePlayer.AddHoleCard(_dealer.DealCard());
                gamePlayer.AddBoardCard(_dealer.DealCard());
            }
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        if (_useBringIn)
        {
            _bringInPlayerIndex = FindBringInPlayer();
        }
        else
        {
            _bringInPlayerIndex = -1;
        }
    }

    /// <summary>
    /// Posts the bring-in bet for third street.
    /// </summary>
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

    /// <summary>
    /// Starts the betting round for the current street.
    /// </summary>
    public void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        int startPosition;
        int minBet;
        int initialBet = 0;
        int forcedBetPlayerIndex = -1;

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
    /// Gets the bring-in player for third street.
    /// </summary>
    public GoodBadUglyGamePlayer GetBringInPlayer()
    {
        if (_bringInPlayerIndex < 0 || _bringInPlayerIndex >= _gamePlayers.Count)
        {
            return null;
        }
        return _gamePlayers[_bringInPlayerIndex];
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

        switch (CurrentPhase)
        {
            case Phases.ThirdStreet:
                CurrentPhase = Phases.FourthStreet;
                break;
            case Phases.FourthStreet:
                // After 4th street betting → reveal The Good
                CurrentPhase = Phases.RevealTheGood;
                break;
            case Phases.RevealTheGood:
                CurrentPhase = Phases.FifthStreet;
                break;
            case Phases.FifthStreet:
                // After 5th street betting → reveal The Bad
                CurrentPhase = Phases.RevealTheBad;
                break;
            case Phases.RevealTheBad:
                CurrentPhase = Phases.SixthStreet;
                break;
            case Phases.SixthStreet:
                // After 6th street betting → reveal The Ugly
                CurrentPhase = Phases.RevealTheUgly;
                break;
            case Phases.RevealTheUgly:
                CurrentPhase = Phases.SeventhStreet;
                break;
            case Phases.SeventhStreet:
                CurrentPhase = Phases.Showdown;
                break;
        }
    }

    /// <summary>
    /// Deals one card for the current street (4th-7th street).
    /// </summary>
    public void DealStreetCard()
    {
        if (CurrentPhase is not (Phases.FourthStreet
            or Phases.FifthStreet
            or Phases.SixthStreet
            or Phases.SeventhStreet))
        {
            throw new InvalidOperationException("Cannot deal street card in current phase");
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                if (CurrentPhase == Phases.SeventhStreet)
                {
                    gamePlayer.AddHoleCard(_dealer.DealCard());
                }
                else
                {
                    gamePlayer.AddBoardCard(_dealer.DealCard());
                }
            }
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }
    }

    /// <summary>
    /// Reveals "The Good" table card. Any cards matching this rank become wild.
    /// Called after the second betting round (4th street).
    /// </summary>
    /// <returns>The revealed Good card.</returns>
    public Card RevealTheGood()
    {
        if (CurrentPhase != Phases.RevealTheGood)
        {
            throw new InvalidOperationException("Cannot reveal The Good in current phase");
        }

        var goodCard = _tableCards[0];
        _wildRank = goodCard.Value;

        // Advance to next street
        CurrentPhase = Phases.FifthStreet;
        return goodCard;
    }

    /// <summary>
    /// Reveals "The Bad" table card. Players must discard all cards matching this rank.
    /// Called after the third betting round (5th street).
    /// </summary>
    /// <returns>A dictionary of player name to their discarded cards.</returns>
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

        // Advance to next street
        CurrentPhase = Phases.SixthStreet;
        return (badCard, discards);
    }

    /// <summary>
    /// Reveals "The Ugly" table card. Any player with a matching face-up card is eliminated.
    /// Called after the fourth betting round (6th street).
    /// </summary>
    /// <returns>The ugly card and list of eliminated player names.</returns>
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
            if (!gamePlayer.Player.HasFolded && gamePlayer.HasMatchingBoardCard(uglyCard.Value))
            {
                gamePlayer.EliminateByUgly();
                eliminated.Add(gamePlayer.Player.Name);
            }
        }

        _eliminatedPlayers.AddRange(eliminated);

        // Check if only one player remains after elimination
        var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
        if (playersInHand <= 1)
        {
            CurrentPhase = Phases.Showdown;
        }
        else
        {
            CurrentPhase = Phases.SeventhStreet;
        }

        return (uglyCard, eliminated);
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// Wild cards from "The Good" are applied during hand evaluation.
    /// </summary>
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

        // If only one player remains, they win by default
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
                    { winner.Player.Name, (null, winner.AllCards.ToList()) }
                },
                WonByFold = true,
                TableCards = _tableCards.AsReadOnly(),
                WildRank = _wildRank,
                DiscardRank = _discardRank,
                EliminationRank = _eliminationRank,
                EliminatedPlayers = _eliminatedPlayers.AsReadOnly()
            };
        }

        // Evaluate hands with wild cards from The Good
        var wildCardRules = new GoodBadUglyWildCardRules();
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp =>
            {
                var allCards = gp.AllCards.ToList();
                var holeCards = gp.HoleCards.Take(2).ToList();
                var boardCards = gp.BoardCards.ToList();
                var downCards = gp.HoleCards.Skip(2).ToList();

                var hand = new GoodBadUglyHand(
                    holeCards,
                    boardCards,
                    downCards,
                    _wildRank,
                    wildCardRules);

                return (hand: (StudHand)hand, cards: (IReadOnlyCollection<Card>)allCards);
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
            EliminatedPlayers = _eliminatedPlayers.AsReadOnly()
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

    /// <summary>
    /// Gets the street name for display purposes.
    /// </summary>
    public string GetCurrentStreetName()
    {
        return CurrentPhase switch
        {
            Phases.ThirdStreet => "Third Street",
            Phases.FourthStreet => "Fourth Street",
            Phases.RevealTheGood => "Reveal The Good",
            Phases.FifthStreet => "Fifth Street",
            Phases.RevealTheBad => "Reveal The Bad",
            Phases.SixthStreet => "Sixth Street",
            Phases.RevealTheUgly => "Reveal The Ugly",
            Phases.SeventhStreet => "Seventh Street (River)",
            _ => CurrentPhase.ToString()
        };
    }

    #region Private Helper Methods

    private int FindBringInPlayer()
    {
        int lowestIndex = -1;
        Card lowestCard = null;

        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            var gamePlayer = _gamePlayers[i];
            if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
            {
                continue;
            }

            var upcard = gamePlayer.BoardCards[0];

            if (lowestCard is null || CompareCardsForBringIn(upcard, lowestCard) < 0)
            {
                lowestCard = upcard;
                lowestIndex = i;
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
        int bestIndex = -1;
        long bestStrength = -1;

        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            var gamePlayer = _gamePlayers[i];
            if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
            {
                continue;
            }

            var visibleStrength = EvaluateVisibleHand(gamePlayer.BoardCards);

            if (visibleStrength > bestStrength)
            {
                bestStrength = visibleStrength;
                bestIndex = i;
            }
        }

        return bestIndex >= 0 ? bestIndex : 0;
    }

    private static long EvaluateVisibleHand(IReadOnlyCollection<Card> boardCards)
    {
        if (boardCards.Count == 0) return 0;

        var cards = boardCards.OrderByDescending(c => c.Value).ToList();
        var valueCounts = cards.GroupBy(c => c.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToList();

        long strength = 0;

        var maxCount = valueCounts.First().Count();

        if (maxCount >= 4)
        {
            strength = 7_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 3)
        {
            strength = 4_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 2)
        {
            var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
            if (pairs.Count >= 2)
            {
                strength = 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10;
            }
            else
            {
                strength = 2_000_000 + pairs[0].Key * 1000;
            }
        }
        else
        {
            strength = 1_000_000;
        }

        foreach (var card in cards.Take(4))
        {
            strength = strength * 15 + card.Value;
        }

        return strength;
    }

    #endregion
}
