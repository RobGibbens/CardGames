using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Games.TwosJacksManWithTheAxe;

/// <summary>
/// Orchestrates a Twos, Jacks, Man with the Axe poker game with betting.
/// </summary>
[PokerGameMetadata(
	"Twos, Jacks, Man with the Axe",
	"A five-card draw variant where all 2s, all Jacks, and the King of Diamonds (“Man with the Axe”) are wild, creating big hands and frequent action. Optionally, a player holding a natural pair of 7s can claim half the pot.",
	2,
	6,
	"2sJacksManWithTheAxe.png")]
public class TwosJacksManWithTheAxe : IPokerGame
{
	public string Name { get; } = "Twos, Jacks, Man with the Axe";
	public string Description { get; } = "A five-card draw variant where all 2s, all Jacks, and the King of Diamonds (“Man with the Axe”) are wild, creating big hands and frequent action. Optionally, a player holding a natural pair of 7s can claim half the pot.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 6;

	private readonly List<TwosJacksManWithTheAxeGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _minBet;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private int _currentDrawPlayerIndex;
    private HashSet<int> _playersWhoHaveDrawn;

    /// <summary>
    /// Gets the current phase of the poker hand being played.
    /// The phase determines which actions are valid and guides the game flow
    /// from dealing through betting rounds to showdown.
    /// </summary>
    public TwosJacksManWithTheAxePhase CurrentPhase { get; private set; }

    /// <summary>
    /// Gets the list of all game players with their hands and game-specific state.
    /// Use this to access player cards and per-hand information.
    /// This collection maintains the original seating order throughout the game session.
    /// </summary>
    public IReadOnlyList<TwosJacksManWithTheAxeGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();

    /// <summary>
    /// Gets the list of underlying poker players with their chip stacks and betting state.
    /// Use this for player identification and chip management.
    /// This is a convenience accessor that extracts <see cref="PokerPlayer"/> instances from <see cref="GamePlayers"/>.
    /// </summary>
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();

    /// <summary>
    /// Gets the total chip amount currently in the pot.
    /// This includes all antes and bets from the current hand, representing
    /// the prize pool that will be awarded at showdown.
    /// </summary>
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;

    /// <summary>
    /// Gets the current betting round manager, or <c>null</c> if no betting round is active.
    /// The betting round tracks whose turn it is to act, the current bet to call,
    /// and validates betting actions according to poker rules.
    /// </summary>
    public BettingRound CurrentBettingRound => _currentBettingRound;

    /// <summary>
    /// Gets the zero-based index of the current dealer in the <see cref="GamePlayers"/> list.
    /// The dealer position rotates clockwise after each hand and determines
    /// the order of play for betting and drawing.
    /// </summary>
    public int DealerPosition => _dealerPosition;

    /// <summary>
    /// Gets the pot manager that tracks all player contributions and calculates side pots.
    /// The pot manager handles complex scenarios where players go all-in for different amounts,
    /// ensuring correct pot distribution at showdown.
    /// </summary>
    public PotManager PotManager => _potManager;

    /// <summary>
    /// Gets the ante amount required from each player before cards are dealt.
    /// The ante creates an initial pot and incentivizes action,
    /// as configured when the game was created.
    /// </summary>
    public int Ante => _ante;

    /// <summary>
    /// Initializes a new Twos, Jacks, Man with the Axe poker game with the specified players and betting parameters.
    /// Creates player instances, initializes the deck dealer, and sets the game to the waiting state.
    /// </summary>
    /// <param name="players">A collection of tuples containing each player's name and starting chip stack.
    /// Must contain between 2 and 6 players due to deck size constraints.</param>
    /// <param name="ante">The mandatory bet amount each player must post before receiving cards.
    /// This creates the initial pot and ensures action in every hand.</param>
    /// <param name="minBet">The minimum bet size allowed during betting rounds.
    /// All raises must be at least this amount.</param>
    /// <exception cref="ArgumentException">Thrown when player count is less than 2 or greater than 6.</exception>
    public TwosJacksManWithTheAxe(IEnumerable<(string name, int chips)> players, int ante, int minBet)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinimumNumberOfPlayers)
        {
            throw new ArgumentException($"Twos, Jacks, Man with the Axe requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"Twos, Jacks, Man with the Axe supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new TwosJacksManWithTheAxeGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _minBet = minBet;
        _dealerPosition = 0;
        CurrentPhase = TwosJacksManWithTheAxePhase.WaitingToStart;
    }

    /// <summary>
    /// Starts a new hand by shuffling the deck, creating a fresh pot, and resetting all player states.
    /// This method must be called before each hand to prepare the game for play.
    /// After calling this method, the game transitions to the <see cref="TwosJacksManWithTheAxePhase.CollectingAntes"/> phase.
    /// </summary>
    /// <remarks>
    /// This method performs the following actions:
    /// <list type="bullet">
    /// <item><description>Shuffles the deck to randomize card distribution</description></item>
    /// <item><description>Creates a new pot manager for tracking bets</description></item>
    /// <item><description>Resets each player's hand and betting state from the previous hand</description></item>
    /// </list>
    /// </remarks>
    public void StartHand()
    {
        // Reset for new hand
        _dealer.Shuffle();
        _potManager = new PotManager();

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetForNewHand();
            gamePlayer.ResetHand();
        }

        CurrentPhase = TwosJacksManWithTheAxePhase.CollectingAntes;
    }

    /// <summary>
    /// Collects the mandatory ante bet from all players to seed the pot before dealing.
    /// Each player contributes the ante amount (or their remaining chips if short-stacked).
    /// After collection, the game transitions to the <see cref="TwosJacksManWithTheAxePhase.Dealing"/> phase.
    /// </summary>
    /// <returns>
    /// A list of <see cref="BettingAction"/> objects representing each player's ante contribution,
    /// useful for displaying the ante collection to users or logging game history.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called outside the <see cref="TwosJacksManWithTheAxePhase.CollectingAntes"/> phase.
    /// </exception>
    /// <remarks>
    /// Players with fewer chips than the ante will contribute their entire stack (going all-in on the ante).
    /// Players with zero chips will not contribute to the pot.
    /// </remarks>
    public List<BettingAction> CollectAntes()
    {
        if (CurrentPhase != TwosJacksManWithTheAxePhase.CollectingAntes)
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

        CurrentPhase = TwosJacksManWithTheAxePhase.Dealing;
        return actions;
    }

    /// <summary>
    /// Deals five cards to each active player from the shuffled deck and initiates the first betting round.
    /// After dealing, the game transitions to the <see cref="TwosJacksManWithTheAxePhase.FirstBettingRound"/> phase.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called outside the <see cref="TwosJacksManWithTheAxePhase.Dealing"/> phase.
    /// </exception>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item><description>Deals 5 cards to each player who has not folded</description></item>
    /// <item><description>Resets all players' current bet amounts for the new betting round</description></item>
    /// <item><description>Automatically starts the first betting round</description></item>
    /// </list>
    /// </remarks>
    public void DealHands()
    {
        if (CurrentPhase != TwosJacksManWithTheAxePhase.Dealing)
        {
            throw new InvalidOperationException("Cannot deal in current phase");
        }

        // Deal 5 cards to each player
        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                var cards = _dealer.DealCards(5);
                gamePlayer.SetHand(cards);
            }
        }

        // Reset current bets before first betting round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        CurrentPhase = TwosJacksManWithTheAxePhase.FirstBettingRound;
        StartBettingRound();
    }

    private void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        _currentBettingRound = new BettingRound(activePlayers, _potManager, _dealerPosition, _minBet);
    }

    /// <summary>
    /// Gets the betting actions available to the current player during an active betting round.
    /// This method determines which actions (check, bet, call, raise, fold, all-in) are valid
    /// based on the current game state and the player's chip stack.
    /// </summary>
    /// <returns>
    /// An <see cref="AvailableActions"/> object containing the valid actions and bet amounts,
    /// or <c>null</c> if no betting round is currently active.
    /// </returns>
    /// <remarks>
    /// Use this method to populate UI elements showing valid player choices.
    /// The available actions depend on whether there's a bet to call, the player's remaining chips,
    /// and standard poker betting rules.
    /// </remarks>
    public AvailableActions GetAvailableActions()
    {
        if (_currentBettingRound == null)
        {
            return null;
        }
        return _currentBettingRound.GetAvailableActions();
    }

    /// <summary>
    /// Gets the player who must act next during the current betting round.
    /// This identifies whose turn it is to make a betting decision.
    /// </summary>
    /// <returns>
    /// The <see cref="PokerPlayer"/> who needs to act, or <c>null</c> if no betting round is active.
    /// </returns>
    /// <remarks>
    /// Betting order follows standard poker rules: action proceeds clockwise from the dealer position,
    /// skipping players who have folded or are all-in.
    /// </remarks>
    public PokerPlayer GetCurrentPlayer()
    {
        return _currentBettingRound?.CurrentPlayer;
    }

    /// <summary>
    /// Processes a betting action from the current player and advances the game state accordingly.
    /// If the betting round completes (all players have acted and bets are equalized), 
    /// the game automatically advances to the next phase.
    /// </summary>
    /// <param name="actionType">The type of betting action to perform (check, bet, call, raise, fold, or all-in).</param>
    /// <param name="amount">The chip amount for bet or raise actions. Ignored for check, call, fold, and all-in actions.
    /// For raises, this is the total amount to put in, not the raise increment.</param>
    /// <returns>
    /// A <see cref="BettingRoundResult"/> indicating whether the action succeeded, any error messages,
    /// and whether the betting round is complete.
    /// </returns>
    /// <remarks>
    /// When the betting round completes:
    /// <list type="bullet">
    /// <item><description>After the first betting round: transitions to draw phase</description></item>
    /// <item><description>After the second betting round: transitions to showdown</description></item>
    /// <item><description>If only one player remains (all others folded): transitions directly to showdown</description></item>
    /// </list>
    /// </remarks>
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
            CurrentPhase = TwosJacksManWithTheAxePhase.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case TwosJacksManWithTheAxePhase.FirstBettingRound:
                CurrentPhase = TwosJacksManWithTheAxePhase.DrawPhase;
                _currentDrawPlayerIndex = FindFirstActivePlayerAfterDealer();
                _playersWhoHaveDrawn = [];
                break;

            case TwosJacksManWithTheAxePhase.SecondBettingRound:
                // Calculate side pots if needed
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = TwosJacksManWithTheAxePhase.Showdown;
                break;
        }
    }

    private int FindFirstActivePlayerAfterDealer()
    {
        var index = (_dealerPosition + 1) % _gamePlayers.Count;
        var count = 0;
        while (count < _gamePlayers.Count)
        {
            if (!_gamePlayers[index].Player.HasFolded && !_gamePlayers[index].Player.IsAllIn)
            {
                return index;
            }
            index = (index + 1) % _gamePlayers.Count;
            count++;
        }
        return -1;
    }

    /// <summary>
    /// Gets the player who must act next during the draw phase.
    /// This identifies whose turn it is to discard and draw cards.
    /// </summary>
    /// <returns>
    /// The <see cref="TwosJacksManWithTheAxeGamePlayer"/> who needs to draw, or <c>null</c> if not in the draw phase
    /// or no eligible players remain (all have drawn, folded, or are all-in).
    /// </returns>
    /// <remarks>
    /// Draw order proceeds clockwise from the dealer position, starting with the first active player.
    /// Players who have folded or are all-in are automatically skipped.
    /// </remarks>
    public TwosJacksManWithTheAxeGamePlayer GetCurrentDrawPlayer()
    {
        if (CurrentPhase != TwosJacksManWithTheAxePhase.DrawPhase || _currentDrawPlayerIndex < 0)
        {
            return null;
        }
        return _gamePlayers[_currentDrawPlayerIndex];
    }

    /// <summary>
    /// Processes a draw action for the current player, allowing them to discard unwanted cards
    /// and receive replacement cards from the deck. After all players have drawn,
    /// the game automatically advances to the second betting round.
    /// </summary>
    /// <param name="discardIndices">Zero-based indices (0-4) of the cards to discard from the player's hand.
    /// Pass an empty collection to "stand pat" (keep all cards). Maximum of 3 cards can be discarded,
    /// or 4 cards if the player holds at least one Ace.</param>
    /// <returns>
    /// A <see cref="DrawResult"/> containing the operation outcome, discarded cards, new cards received,
    /// and whether the draw phase is complete.
    /// </returns>
    /// <remarks>
    /// Standard Twos, Jacks, Man with the Axe rules apply:
    /// <list type="bullet">
    /// <item><description>Players may discard 0-3 cards (or 0-4 if the player holds at least one Ace)</description></item>
    /// <item><description>Discarded cards are replaced with new cards from the deck</description></item>
    /// <item><description>Card indices must be between 0 and 4 (inclusive)</description></item>
    /// </list>
    /// The method automatically advances to the next player or to the second betting round
    /// when all eligible players have completed their draws.
    /// </remarks>
    public DrawResult ProcessDraw(IReadOnlyCollection<int> discardIndices)
    {
        if (CurrentPhase != TwosJacksManWithTheAxePhase.DrawPhase)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Not in draw phase"
            };
        }

        var gamePlayer = _gamePlayers[_currentDrawPlayerIndex];

        // Allow 4 discards if the player has an Ace in their current hand (pre-discard)
        var hasAce = gamePlayer.Hand.Any(c => c.Symbol == Symbol.Ace);
        var maxDiscards = hasAce ? 4 : 3;

        if (discardIndices.Count > maxDiscards)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = hasAce
                    ? "Cannot discard more than 4 cards"
                    : "Cannot discard more than 3 cards"
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
            DrawComplete = CurrentPhase != TwosJacksManWithTheAxePhase.DrawPhase
        };
    }

    private void MoveToNextDrawPlayer()
    {
        // Find next eligible player who hasn't drawn yet
        var startIndex = _currentDrawPlayerIndex;
        _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
        var checkedCount = 0;

        while (checkedCount < _gamePlayers.Count)
        {
            var player = _gamePlayers[_currentDrawPlayerIndex].Player;
            
            // Check if this player can act and hasn't drawn yet
            if (!player.HasFolded && !player.IsAllIn && !_playersWhoHaveDrawn.Contains(_currentDrawPlayerIndex))
            {
                return;
            }

            _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
            checkedCount++;
        }

        // All players have drawn - move to second betting round
        StartSecondBettingRound();
    }

    private void StartSecondBettingRound()
    {
        CurrentPhase = TwosJacksManWithTheAxePhase.SecondBettingRound;
        StartBettingRound();
    }

    /// <summary>
    /// Manually completes the draw phase and advances to the second betting round.
    /// This is a fallback method that can be called to force progression if the automatic
    /// advancement did not occur (e.g., when all remaining players are all-in).
    /// </summary>
    /// <remarks>
    /// Normally, the draw phase completes automatically when all eligible players have called
    /// <see cref="ProcessDraw"/>. This method exists for edge cases where manual progression
    /// is needed, such as when all players chose to stand pat or are all-in.
    /// If not currently in the draw phase, this method has no effect.
    /// </remarks>
    public void CompleteDrawPhase()
    {
        if (CurrentPhase == TwosJacksManWithTheAxePhase.DrawPhase)
        {
            StartSecondBettingRound();
        }
    }

    /// <summary>
    /// Performs the showdown to determine the winner(s) and distribute the pot accordingly.
    /// Evaluates all remaining players' hands, identifies the winner(s), awards chips,
    /// and advances the dealer button for the next hand.
    /// </summary>
    /// <returns>
    /// A <see cref="ShowdownResult"/> containing the operation outcome, pot distributions to each winner,
    /// evaluated hand information for all participating players, and whether the hand was won by fold.
    /// </returns>
    /// <remarks>
    /// The showdown handles several scenarios:
    /// <list type="bullet">
    /// <item><description><b>Win by fold:</b> If only one player remains, they win the entire pot without showing cards</description></item>
    /// <item><description><b>Single winner:</b> The player with the highest-ranking hand wins the entire pot</description></item>
    /// <item><description><b>Split pot:</b> If multiple players tie, the pot is divided equally among winners</description></item>
    /// <item><description><b>Side pots:</b> When players are all-in for different amounts, side pots are calculated and awarded separately</description></item>
    /// </list>
    /// After the showdown, the game phase becomes <see cref="TwosJacksManWithTheAxePhase.Complete"/> and the dealer button moves.
    /// </remarks>
    public ShowdownResult PerformShowdown()
    {
        if (CurrentPhase != TwosJacksManWithTheAxePhase.Showdown)
        {
            return new ShowdownResult
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

            CurrentPhase = TwosJacksManWithTheAxePhase.Complete;
            MoveDealer();

            return new ShowdownResult
            {
                Success = true,
                Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
                PlayerHands = new Dictionary<string, (DrawHand hand, IReadOnlyCollection<Card> cards)>
                {
                    { winner.Player.Name, (null, winner.Hand) }
                },
                WonByFold = true
            };
        }

        // Evaluate hands
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp => (hand: new DrawHand(gp.Hand), cards: (IReadOnlyCollection<Card>)gp.Hand)
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

        CurrentPhase = TwosJacksManWithTheAxePhase.Complete;
        MoveDealer();

        return new ShowdownResult
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
    /// Gets all players who have chips remaining and can participate in future hands.
    /// This is used to determine which players are still active in the game session.
    /// </summary>
    /// <returns>
    /// An enumerable of <see cref="PokerPlayer"/> objects representing players with a positive chip stack.
    /// </returns>
    /// <remarks>
    /// Players are eliminated from the game when their chip stack reaches zero.
    /// Use this method in conjunction with <see cref="CanContinue"/> to determine if the game can proceed.
    /// </remarks>
    public IEnumerable<PokerPlayer> GetPlayersWithChips()
    {
        return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
    }

    /// <summary>
    /// Determines whether the game can continue with another hand.
    /// A game requires at least two players with chips to proceed.
    /// </summary>
    /// <returns>
    /// <c>true</c> if at least two players have chips remaining and another hand can be played;
    /// <c>false</c> if the game has concluded with a single winner or all players are eliminated.
    /// </returns>
    /// <remarks>
    /// Call this method after each hand completes to determine if the game session should continue.
    /// When this returns <c>false</c>, the remaining player with chips (if any) is the overall game winner.
    /// </remarks>
    public bool CanContinue()
    {
        return GetPlayersWithChips().Count() >= 2;
    }
}