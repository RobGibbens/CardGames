using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Orchestrates a Kings and Lows poker game.
/// 
/// Kings and Lows is a five-card draw variant with special rules:
/// - Kings are always wild
/// - The lowest-ranked card in each player's hand is also wild
/// - No traditional betting rounds - just antes, drop-or-stay, and pot matching
/// - Losers match the pot after showdown
/// </summary>
[PokerGameMetadata(
    code:"KINGSANDLOWS",
	name:"Kings and Lows",
	description:"A five-card draw poker variant where kings and the lowest card are wild. Players ante, decide to drop or stay, draw cards, and losers match the pot.",
	minimumNumberOfPlayers:2,
	maximumNumberOfPlayers:5,
    initialHoleCards:5,
    initialBoardCards:0,
    maxCommunityCards:0,
    maxPlayerCards:5,
    hasDrawPhase:true,
    maxDiscards:5,
    wildCardRule:WildCardRule.LowestCard,
	bettingStructure:BettingStructure.AntePotMatch,
	imageName:"kingsandlows.png")]
public class KingsAndLowsGame : IPokerGame
{
	public string Name { get; } = "Kings and Lows";
	public string Description { get; } = "A five-card draw poker variant where kings and the lowest card are wild. Players ante, decide to drop or stay, draw cards, and losers match the pot.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 5;

	private readonly List<KingsAndLowsGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly WildCardRules _wildCardRules;
    private readonly bool _anteEveryHand;

    private int _currentPot;
    private int _dealerPosition;
    private int _currentDrawPlayerIndex;
    private HashSet<int> _playersWhoHaveDrawn;
    private bool _initialAntePaid;

    // For player vs deck scenario
    private List<Card> _deckHand;
    private bool _deckHandDrawComplete;

    // Track which players need to match pot
    private List<string> _losers;
    private Dictionary<string, int> _potMatchAmounts;

    public Phases CurrentPhase { get; private set; }
    public IReadOnlyList<KingsAndLowsGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int CurrentPot => _currentPot;
    public int DealerPosition => _dealerPosition;
    public int Ante => _ante;
    public WildCardRules WildCardRules => _wildCardRules;

    /// <summary>
    /// The deck's hand when playing player vs deck.
    /// </summary>
    public IReadOnlyList<Card> DeckHand => _deckHand?.AsReadOnly();

    /// <summary>
    /// Players who lost and need to match the pot.
    /// </summary>
    public IReadOnlyList<string> Losers => _losers?.AsReadOnly();

    /// <summary>
    /// Amount each loser needs to match.
    /// </summary>
    public IReadOnlyDictionary<string, int> PotMatchAmounts => _potMatchAmounts;

    public KingsAndLowsGame(
        IEnumerable<(string name, int chips)> players, 
        int ante, 
        bool kingRequired = false,
        bool anteEveryHand = false)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinimumNumberOfPlayers)
        {
            throw new ArgumentException($"Kings and Lows requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"Kings and Lows supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new KingsAndLowsGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _wildCardRules = new WildCardRules(kingRequired);
        _anteEveryHand = anteEveryHand;
        _dealerPosition = 0;
        _currentPot = 0;
        _initialAntePaid = false;
        CurrentPhase = Phases.WaitingToStart;
    }

    /// <summary>
    /// Gets the game rules metadata for Kings and Lows.
    /// </summary>
    public GameRules GetGameRules()
    {
        return KingsAndLowsRules.CreateGameRules();
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
    public void StartHand()
    {
        _dealer.Shuffle();
        _deckHand = null;
        _deckHandDrawComplete = false;
        _losers = null;
        _potMatchAmounts = null;

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.ResetForNewHand();
            gamePlayer.Player.ResetForNewHand();
        }

        // Determine if we need to collect antes
        if (!_initialAntePaid || _anteEveryHand)
        {
            CurrentPhase = Phases.CollectingAntes;
        }
        else
        {
            // Skip ante collection, go straight to dealing
            CurrentPhase = Phases.Dealing;
        }
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
                _currentPot += actualAmount;
                actions.Add(new BettingAction(player.Name, BettingActionType.Post, actualAmount));
            }
        }

        _initialAntePaid = true;
        CurrentPhase = Phases.Dealing;
        return actions;
    }

    /// <summary>
    /// Deals initial hands to all players (5 cards each).
    /// </summary>
    public void DealHands()
    {
        if (CurrentPhase != Phases.Dealing)
        {
            throw new InvalidOperationException("Cannot deal in current phase");
        }

        // Deal 5 cards to each player
        foreach (var gamePlayer in _gamePlayers)
        {
            var cards = _dealer.DealCards(5);
            gamePlayer.SetHand(cards);
        }

        CurrentPhase = Phases.DropOrStay;
    }

    /// <summary>
    /// Records a player's drop-or-stay decision.
    /// </summary>
    public void SetPlayerDecision(string playerName, DropOrStayDecision decision)
    {
        if (CurrentPhase != Phases.DropOrStay)
        {
            throw new InvalidOperationException("Cannot set decision in current phase");
        }

        var gamePlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == playerName);
        if (gamePlayer == null)
        {
            throw new ArgumentException($"Player '{playerName}' not found");
        }

        gamePlayer.SetDecision(decision);
    }

    /// <summary>
    /// Gets players who haven't made their drop-or-stay decision yet.
    /// </summary>
    public IEnumerable<KingsAndLowsGamePlayer> GetPlayersAwaitingDecision()
    {
        return _gamePlayers.Where(gp => gp.Decision == DropOrStayDecision.Undecided);
    }

    /// <summary>
    /// Checks if all players have made their decisions and advances to the next phase.
    /// </summary>
    public DropOrStayResult FinalizeDropOrStay()
    {
        if (CurrentPhase != Phases.DropOrStay)
        {
            throw new InvalidOperationException("Cannot finalize drop/stay in current phase");
        }

        // Check if all players have decided
        if (_gamePlayers.Any(gp => gp.Decision == DropOrStayDecision.Undecided))
        {
            return new DropOrStayResult
            {
                Success = false,
                ErrorMessage = "Not all players have made their decision"
            };
        }

        var stayingPlayers = _gamePlayers.Where(gp => gp.HasStayed).ToList();
        var droppedPlayers = _gamePlayers.Where(gp => gp.HasDropped).ToList();

        // Mark dropped players as folded
        foreach (var player in droppedPlayers)
        {
            player.Player.Fold();
        }

        // Handle edge cases
        if (stayingPlayers.Count == 0)
        {
            // All players dropped - dead hand, keep pot for next hand
            CurrentPhase = Phases.Complete;
            return new DropOrStayResult
            {
                Success = true,
                AllDropped = true,
                StayingPlayerCount = 0,
                DroppedPlayerNames = droppedPlayers.Select(gp => gp.Player.Name).ToList()
            };
        }
        else if (stayingPlayers.Count == 1)
        {
            // Only one player stayed - player vs deck
            CurrentPhase = Phases.DrawPhase;
            _currentDrawPlayerIndex = _gamePlayers.IndexOf(stayingPlayers[0]);
            _playersWhoHaveDrawn = [];
            return new DropOrStayResult
            {
                Success = true,
                SinglePlayerStayed = true,
                StayingPlayerCount = 1,
                StayingPlayerNames = stayingPlayers.Select(gp => gp.Player.Name).ToList(),
                DroppedPlayerNames = droppedPlayers.Select(gp => gp.Player.Name).ToList()
            };
        }
        else
        {
            // Normal case - multiple players stayed
            CurrentPhase = Phases.DrawPhase;
            _currentDrawPlayerIndex = FindFirstStayingPlayerAfterDealer();
            _playersWhoHaveDrawn = [];
            return new DropOrStayResult
            {
                Success = true,
                StayingPlayerCount = stayingPlayers.Count,
                StayingPlayerNames = stayingPlayers.Select(gp => gp.Player.Name).ToList(),
                DroppedPlayerNames = droppedPlayers.Select(gp => gp.Player.Name).ToList()
            };
        }
    }

    private int FindFirstStayingPlayerAfterDealer()
    {
        var index = (_dealerPosition + 1) % _gamePlayers.Count;
        var count = 0;
        while (count < _gamePlayers.Count)
        {
            if (_gamePlayers[index].HasStayed)
            {
                return index;
            }
            index = (index + 1) % _gamePlayers.Count;
            count++;
        }
        return -1;
    }

    /// <summary>
    /// Gets the current player in the draw phase.
    /// </summary>
    public KingsAndLowsGamePlayer GetCurrentDrawPlayer()
    {
        if (CurrentPhase != Phases.DrawPhase || _currentDrawPlayerIndex < 0)
        {
            return null;
        }
        return _gamePlayers[_currentDrawPlayerIndex];
    }

    /// <summary>
    /// Processes a draw action for the current player.
    /// In Kings and Lows, players can discard up to 5 cards.
    /// </summary>
    public DrawResult ProcessDraw(IReadOnlyCollection<int> discardIndices)
    {
        if (CurrentPhase != Phases.DrawPhase)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Not in draw phase"
            };
        }

        var gamePlayer = _gamePlayers[_currentDrawPlayerIndex];

        if (discardIndices.Count > 5)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Cannot discard more than 5 cards"
            };
        }

        if (discardIndices.Any(i => i < 0 || i >= 5))
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Invalid card index (must be 0-4)"
            };
        }

        // Deal new cards
        var newCards = _dealer.DealCards(discardIndices.Count);
        var discardedCards = discardIndices.Select(i => gamePlayer.Hand[i]).ToList();
        gamePlayer.DiscardAndDraw(discardIndices, newCards);

        // Track that this player has drawn
        _playersWhoHaveDrawn.Add(_currentDrawPlayerIndex);

        // Move to next player or next phase
        MoveToNextDrawPlayer();

        return new DrawResult
        {
            Success = true,
            PlayerName = gamePlayer.Player.Name,
            DiscardedCards = discardedCards,
            NewCards = newCards.ToList(),
            DrawComplete = CurrentPhase != Phases.DrawPhase
        };
    }

    private void MoveToNextDrawPlayer()
    {
        // Find next staying player who hasn't drawn yet
        var startIndex = _currentDrawPlayerIndex;
        _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
        var checkedCount = 0;

        while (checkedCount < _gamePlayers.Count)
        {
            var player = _gamePlayers[_currentDrawPlayerIndex];
            
            // Check if this player has stayed and hasn't drawn yet
            if (player.HasStayed && !_playersWhoHaveDrawn.Contains(_currentDrawPlayerIndex))
            {
                return;
            }

            _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
            checkedCount++;
        }

        // All players have drawn - determine next phase
        var stayingCount = _gamePlayers.Count(gp => gp.HasStayed);
        if (stayingCount == 1)
        {
            // Single player stayed - go to player vs deck
            CurrentPhase = Phases.PlayerVsDeck;
            DealDeckHand();
        }
        else
        {
            // Multiple players - go to showdown
            CurrentPhase = Phases.Showdown;
        }
    }

    /// <summary>
    /// Deals a hand for the deck (in player vs deck scenario).
    /// </summary>
    private void DealDeckHand()
    {
        _deckHand = _dealer.DealCards(5).ToList();
        _deckHandDrawComplete = false;
    }

    /// <summary>
    /// Gets the deck's hand wild cards.
    /// </summary>
    public IReadOnlyCollection<Card> GetDeckHandWildCards()
    {
        if (_deckHand == null)
        {
            return [];
        }
        return _wildCardRules.DetermineWildCards(_deckHand);
    }

    /// <summary>
    /// Processes a manual draw for the deck hand (in player vs deck scenario).
    /// The dealer chooses which cards to discard for the deck.
    /// </summary>
    public DrawResult ProcessDeckDrawManual(IReadOnlyCollection<int> discardIndices)
    {
        if (CurrentPhase != Phases.PlayerVsDeck)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Not in player vs deck phase"
            };
        }

        if (_deckHandDrawComplete)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Deck has already drawn"
            };
        }

        if (discardIndices.Count > 5)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Cannot discard more than 5 cards"
            };
        }

        if (discardIndices.Any(i => i < 0 || i >= 5))
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Invalid card index (must be 0-4)"
            };
        }

        // Draw replacements
        var newCards = _dealer.DealCards(discardIndices.Count);
        var discardedCards = discardIndices.Select(i => _deckHand[i]).ToList();

        // Remove in descending order
        foreach (var index in discardIndices.OrderByDescending(i => i))
        {
            _deckHand.RemoveAt(index);
        }
        _deckHand.AddRange(newCards);

        _deckHandDrawComplete = true;
        CurrentPhase = Phases.Showdown;

        return new DrawResult
        {
            Success = true,
            PlayerName = "Deck",
            DiscardedCards = discardedCards,
            NewCards = newCards.ToList(),
            DrawComplete = true
        };
    }

    /// <summary>
    /// Processes a draw for the deck hand (in player vs deck scenario).
    /// The deck draws to improve its hand (simple AI: replaces non-wild, non-king cards).
    /// </summary>
    public DrawResult ProcessDeckDraw()
    {
        if (CurrentPhase != Phases.PlayerVsDeck)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Not in player vs deck phase"
            };
        }

        if (_deckHandDrawComplete)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Deck has already drawn"
            };
        }

        // Simple draw strategy: keep wild cards and pairs, discard rest
        var wildCards = _wildCardRules.DetermineWildCards(_deckHand);
        var discardIndices = new List<int>();

        // Group non-wild cards by value
        var nonWildCards = _deckHand.Where(c => !wildCards.Contains(c)).ToList();
        var valueCounts = nonWildCards.GroupBy(c => c.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < _deckHand.Count; i++)
        {
            var card = _deckHand[i];
            // Keep wild cards
            if (wildCards.Contains(card))
            {
                continue;
            }
            // Keep pairs or better
            if (valueCounts.TryGetValue(card.Value, out var count) && count >= 2)
            {
                continue;
            }
            // Discard the rest
            discardIndices.Add(i);
        }

        // Draw replacements
        var newCards = _dealer.DealCards(discardIndices.Count);
        var discardedCards = discardIndices.Select(i => _deckHand[i]).ToList();

        // Remove in descending order
        foreach (var index in discardIndices.OrderByDescending(i => i))
        {
            _deckHand.RemoveAt(index);
        }
        _deckHand.AddRange(newCards);

        _deckHandDrawComplete = true;
        CurrentPhase = Phases.Showdown;

        return new DrawResult
        {
            Success = true,
            PlayerName = "Deck",
            DiscardedCards = discardedCards,
            NewCards = newCards.ToList(),
            DrawComplete = true
        };
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// Returns showdown result including who needs to match the pot.
    /// </summary>
    public KingsAndLowsShowdownResult PerformShowdown()
    {
        if (CurrentPhase != Phases.Showdown)
        {
            return new KingsAndLowsShowdownResult
            {
                Success = false,
                ErrorMessage = "Not in showdown phase"
            };
        }

        var stayingPlayers = _gamePlayers.Where(gp => gp.HasStayed).ToList();
        var isPlayerVsDeck = stayingPlayers.Count == 1 && _deckHand != null;

        // Evaluate hands with wild cards
        var playerHands = stayingPlayers.ToDictionary(
            gp => gp.Player.Name,
            gp => EvaluateHandWithWildCards(gp.Hand)
        );

        // Add deck hand if player vs deck
        if (isPlayerVsDeck)
        {
            playerHands["Deck"] = EvaluateHandWithWildCards(_deckHand);
        }

        // Find the winner
        var maxStrength = playerHands.Values.Max(h => h.strength);
        var winners = playerHands.Where(kvp => kvp.Value.strength == maxStrength)
            .Select(kvp => kvp.Key).ToList();

        // Determine losers (players who need to match the pot)
        _losers = playerHands.Keys.Where(name => !winners.Contains(name) && name != "Deck").ToList();

        // Calculate pot distribution
        var potToDistribute = _currentPot;
        var payouts = new Dictionary<string, int>();

        if (winners.Contains("Deck"))
        {
            // Deck wins - player loses and must match pot
            // The pot stays for the next hand
            var playerName = stayingPlayers[0].Player.Name;
            _losers = new List<string> { playerName };
        }
        else if (winners.All(w => w != "Deck"))
        {
            // Player(s) win
            var sharePerWinner = potToDistribute / winners.Count;
            var remainder = potToDistribute % winners.Count;

            foreach (var winner in winners)
            {
                var payout = sharePerWinner;
                if (remainder > 0)
                {
                    payout++;
                    remainder--;
                }
                payouts[winner] = payout;

                var gamePlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == winner);
                gamePlayer?.Player.AddChips(payout);
            }

            _currentPot = 0;
        }

        // Calculate how much each loser needs to match
        var potBeforeShowdown = potToDistribute;
        _potMatchAmounts = new Dictionary<string, int>();
        foreach (var loser in _losers)
        {
            var loserPlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == loser);
            if (loserPlayer != null)
            {
                var matchAmount = Math.Min(potBeforeShowdown, loserPlayer.Player.ChipStack);
                _potMatchAmounts[loser] = matchAmount;
            }
        }

        // Move to pot matching phase if there are losers
        if (_losers.Any())
        {
            CurrentPhase = Phases.PotMatching;
        }
        else
        {
            CurrentPhase = Phases.Complete;
            MoveDealer();
        }

        return new KingsAndLowsShowdownResult
        {
            Success = true,
            Payouts = payouts,
            PlayerHands = playerHands.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.hand, kvp.Value.cards)
            ),
            IsPlayerVsDeck = isPlayerVsDeck,
            DeckWon = winners.Contains("Deck"),
            IsTie = winners.Count > 1 && !winners.Contains("Deck"),
            Winners = winners.Where(w => w != "Deck").ToList(),
            Losers = _losers.ToList(),
            PotMatchAmounts = _potMatchAmounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            PotBeforeShowdown = potBeforeShowdown
        };
    }

    private (DrawHand hand, long strength, IReadOnlyCollection<Card> cards) EvaluateHandWithWildCards(IReadOnlyCollection<Card> cards)
    {
        var wildCards = _wildCardRules.DetermineWildCards(cards);
        
        if (wildCards.Any())
        {
            var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
                cards, wildCards, Hands.Strength.HandTypeStrengthRanking.Classic);
            
            // Create a DrawHand for display purposes (using evaluated cards)
            var hand = new DrawHand(evaluatedCards);
            return (hand, strength, cards);
        }
        else
        {
            var hand = new DrawHand(cards);
            return (hand, hand.Strength, cards);
        }
    }

    /// <summary>
    /// Processes pot matching from losers.
    /// Each loser must match the pot amount (or go all-in if short-stacked).
    /// </summary>
    public PotMatchResult ProcessPotMatching()
    {
        if (CurrentPhase != Phases.PotMatching)
        {
            return new PotMatchResult
            {
                Success = false,
                ErrorMessage = "Not in pot matching phase"
            };
        }

        var matchActions = new List<BettingAction>();
        var totalMatched = 0;

        foreach (var loser in _losers)
        {
            var loserPlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == loser);
            if (loserPlayer != null && _potMatchAmounts.TryGetValue(loser, out var matchAmount))
            {
                if (matchAmount > 0)
                {
                    var actualAmount = loserPlayer.Player.PlaceBet(matchAmount);
                    totalMatched += actualAmount;
                    matchActions.Add(new BettingAction(loser, BettingActionType.Post, actualAmount));
                }
            }
        }

        // The matched amounts form the new pot for the next hand
        _currentPot += totalMatched;

        CurrentPhase = Phases.Complete;
        MoveDealer();

        return new PotMatchResult
        {
            Success = true,
            MatchActions = matchActions,
            NewPotAmount = _currentPot
        };
    }

    /// <summary>
    /// Skips pot matching (for when deck wins in player vs deck and player matches).
    /// </summary>
    public void SkipPotMatching()
    {
        if (CurrentPhase == Phases.PotMatching)
        {
            // In player vs deck loss, the pot stays and loser matches
            if (_losers != null && _losers.Count == 1)
            {
                var loser = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == _losers[0]);
                if (loser != null && _potMatchAmounts.TryGetValue(_losers[0], out var matchAmount))
                {
                    var actualAmount = loser.Player.PlaceBet(matchAmount);
                    _currentPot += actualAmount;
                }
            }
        }
        
        CurrentPhase = Phases.Complete;
        MoveDealer();
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
    /// Gets a player's wild cards based on their current hand.
    /// </summary>
    public IReadOnlyCollection<Card> GetPlayerWildCards(string playerName)
    {
        var gamePlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == playerName);
        if (gamePlayer == null || !gamePlayer.Hand.Any())
        {
            return [];
        }
        return _wildCardRules.DetermineWildCards(gamePlayer.Hand);
    }
}