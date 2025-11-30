#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Games;

/// <summary>
/// Represents a player in a Dealer's Choice game.
/// </summary>
public class DealersChoiceGamePlayer
{
    public PokerPlayer Player { get; }

    public DealersChoiceGamePlayer(PokerPlayer player)
    {
        Player = player;
    }

    public void ResetForNewHand()
    {
        Player.ResetForNewHand();
    }
}

/// <summary>
/// Orchestrates a Dealer's Choice poker game where the dealer selects
/// the variant to play for each hand, with optional wild card configurations.
/// </summary>
public class DealersChoiceGame
{
    private const int MinPlayers = 2;
    private const int MaxPlayers = 10;

    private readonly List<DealersChoiceGamePlayer> _gamePlayers;
    private readonly int _smallBlind;
    private readonly int _bigBlind;
    private readonly bool _allowWildCards;
    private readonly List<PokerVariant> _allowedVariants;

    private int _dealerPosition;
    private DealersChoiceHandConfig? _currentHandConfig;
    private object? _currentGame;
    private int _handsPlayed;

    public DealersChoicePhase CurrentPhase { get; private set; }
    public IReadOnlyList<DealersChoiceGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int DealerPosition => _dealerPosition;
    public int SmallBlind => _smallBlind;
    public int BigBlind => _bigBlind;
    public bool AllowWildCards => _allowWildCards;
    public IReadOnlyList<PokerVariant> AllowedVariants => _allowedVariants.AsReadOnly();
    public DealersChoiceHandConfig? CurrentHandConfig => _currentHandConfig;
    public int HandsPlayed => _handsPlayed;

    /// <summary>
    /// Gets the current variant-specific game instance.
    /// Cast to the appropriate type based on CurrentHandConfig.Variant.
    /// </summary>
    public object? CurrentGame => _currentGame;

    /// <summary>
    /// Gets the current dealer player.
    /// </summary>
    public DealersChoiceGamePlayer CurrentDealer => _gamePlayers[_dealerPosition];

    public DealersChoiceGame(
        IEnumerable<(string name, int chips)> players,
        int smallBlind,
        int bigBlind,
        bool allowWildCards = true,
        IEnumerable<PokerVariant>? allowedVariants = null)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinPlayers)
        {
            throw new ArgumentException($"Dealer's Choice requires at least {MinPlayers} players");
        }

        if (playerList.Count > MaxPlayers)
        {
            throw new ArgumentException($"Dealer's Choice supports at most {MaxPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new DealersChoiceGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _smallBlind = smallBlind;
        _bigBlind = bigBlind;
        _allowWildCards = allowWildCards;
        _allowedVariants = allowedVariants?.ToList() ?? GetDefaultAllowedVariants();
        _dealerPosition = 0;
        _handsPlayed = 0;
        CurrentPhase = DealersChoicePhase.WaitingToStart;
    }

    private static List<PokerVariant> GetDefaultAllowedVariants()
    {
        return
        [
            PokerVariant.TexasHoldem,
            PokerVariant.Omaha,
            PokerVariant.SevenCardStud,
            PokerVariant.FiveCardDraw
        ];
    }

    /// <summary>
    /// Starts a new hand, entering the variant selection phase.
    /// </summary>
    public void StartHand()
    {
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.ResetForNewHand();
        }

        _currentHandConfig = null;
        _currentGame = null;

        CurrentPhase = DealersChoicePhase.SelectingVariant;
    }

    /// <summary>
    /// Gets the allowed variants for the current dealer to choose from.
    /// </summary>
    public IReadOnlyList<PokerVariant> GetAvailableVariants()
    {
        // Filter variants based on current player count
        var playerCount = _gamePlayers.Count(gp => gp.Player.ChipStack > 0);
        return _allowedVariants.Where(v => IsVariantValidForPlayerCount(v, playerCount)).ToList();
    }

    private static bool IsVariantValidForPlayerCount(PokerVariant variant, int playerCount)
    {
        return variant switch
        {
            PokerVariant.TexasHoldem => playerCount >= 2 && playerCount <= 10,
            PokerVariant.Omaha => playerCount >= 2 && playerCount <= 10,
            PokerVariant.SevenCardStud => playerCount >= 2 && playerCount <= 7,
            PokerVariant.FiveCardDraw => playerCount >= 2 && playerCount <= 6,
            PokerVariant.Baseball => playerCount >= 2 && playerCount <= 4,
            PokerVariant.KingsAndLows => playerCount >= 2 && playerCount <= 5,
            PokerVariant.FollowTheQueen => playerCount >= 2 && playerCount <= 7,
            _ => playerCount >= 2 && playerCount <= 10
        };
    }

    /// <summary>
    /// Gets the available wild card types for the dealer to choose from.
    /// </summary>
    public IReadOnlyList<WildCardType> GetAvailableWildCardTypes()
    {
        if (!_allowWildCards)
        {
            return [WildCardType.None];
        }

        return
        [
            WildCardType.None,
            WildCardType.DeucesWild,
            WildCardType.ThreesAndNines,
            WildCardType.KingsWild,
            WildCardType.QueensWild,
            WildCardType.OneEyedJacks,
            WildCardType.JacksWild,
            WildCardType.Custom
        ];
    }

    /// <summary>
    /// Sets the dealer's selected variant for this hand.
    /// </summary>
    public SelectVariantResult SelectVariant(PokerVariant variant)
    {
        if (CurrentPhase != DealersChoicePhase.SelectingVariant)
        {
            return new SelectVariantResult
            {
                Success = false,
                ErrorMessage = "Cannot select variant in current phase"
            };
        }

        if (!_allowedVariants.Contains(variant))
        {
            return new SelectVariantResult
            {
                Success = false,
                ErrorMessage = $"Variant '{variant}' is not allowed in this game"
            };
        }

        var availableVariants = GetAvailableVariants();
        if (!availableVariants.Contains(variant))
        {
            return new SelectVariantResult
            {
                Success = false,
                ErrorMessage = $"Variant '{variant}' does not support the current number of players"
            };
        }

        _currentHandConfig = new DealersChoiceHandConfig
        {
            Variant = variant,
            WildCards = WildCardConfiguration.None()
        };

        // Move to wild card configuration if allowed, otherwise start the hand
        if (_allowWildCards)
        {
            CurrentPhase = DealersChoicePhase.ConfiguringWildCards;
        }
        else
        {
            CreateAndStartGame();
        }

        return new SelectVariantResult
        {
            Success = true,
            SelectedVariant = variant,
            RequiresWildCardConfig = _allowWildCards
        };
    }

    /// <summary>
    /// Sets the dealer's wild card configuration for this hand.
    /// </summary>
    public ConfigureWildCardsResult ConfigureWildCards(WildCardConfiguration config)
    {
        if (CurrentPhase != DealersChoicePhase.ConfiguringWildCards)
        {
            return new ConfigureWildCardsResult
            {
                Success = false,
                ErrorMessage = "Cannot configure wild cards in current phase"
            };
        }

        if (!_allowWildCards && config.Enabled)
        {
            return new ConfigureWildCardsResult
            {
                Success = false,
                ErrorMessage = "Wild cards are not allowed in this game"
            };
        }

        _currentHandConfig.WildCards = config;

        CreateAndStartGame();

        return new ConfigureWildCardsResult
        {
            Success = true,
            Configuration = config
        };
    }

    /// <summary>
    /// Skips wild card configuration (uses no wild cards).
    /// </summary>
    public ConfigureWildCardsResult SkipWildCardConfiguration()
    {
        return ConfigureWildCards(WildCardConfiguration.None());
    }

    private void CreateAndStartGame()
    {
        var activePlayers = _gamePlayers
            .Where(gp => gp.Player.ChipStack > 0)
            .Select(gp => (gp.Player.Name, gp.Player.ChipStack));

        _currentGame = _currentHandConfig.Variant switch
        {
            PokerVariant.TexasHoldem => new HoldEmGame(activePlayers, _smallBlind, _bigBlind),
            PokerVariant.Omaha => new OmahaGame(activePlayers, _smallBlind, _bigBlind),
            PokerVariant.SevenCardStud => new SevenCardStudGame(
                activePlayers,
                ante: _smallBlind,
                bringIn: _bigBlind / 2,
                smallBet: _bigBlind,
                bigBet: _bigBlind * 2,
                useBringIn: true),
            PokerVariant.FiveCardDraw => new FiveCardDrawGame(activePlayers, _smallBlind, _bigBlind),
            PokerVariant.Baseball => new BaseballGame(
                activePlayers,
                ante: _smallBlind,
                bringIn: _bigBlind / 2,
                smallBet: _bigBlind,
                bigBet: _bigBlind * 2,
                buyCardPrice: _bigBlind,
                useBringIn: true),
            PokerVariant.KingsAndLows => new KingsAndLowsGame(
                activePlayers,
                ante: _smallBlind,
                kingRequired: false,
                anteEveryHand: false),
            PokerVariant.FollowTheQueen => new FollowTheQueenGame(
                activePlayers,
                ante: _smallBlind,
                bringIn: _bigBlind / 2,
                smallBet: _bigBlind,
                bigBet: _bigBlind * 2,
                useBringIn: true),
            _ => throw new InvalidOperationException($"Unsupported variant: {_currentHandConfig.Variant}")
        };

        CurrentPhase = DealersChoicePhase.PlayingHand;
    }

    /// <summary>
    /// Gets the current game as the specified type.
    /// </summary>
    public T GetCurrentGameAs<T>() where T : class
    {
        return _currentGame as T;
    }

    /// <summary>
    /// Marks the current hand as complete and advances to the next dealer.
    /// </summary>
    public void CompleteHand()
    {
        if (CurrentPhase != DealersChoicePhase.PlayingHand)
        {
            throw new InvalidOperationException("No hand in progress to complete");
        }

        // Update player chip stacks from the underlying game
        UpdatePlayerChipStacks();

        _handsPlayed++;
        CurrentPhase = DealersChoicePhase.HandComplete;
        MoveDealer();
    }

    private void UpdatePlayerChipStacks()
    {
        if (_currentGame == null) return;

        IReadOnlyList<PokerPlayer>? gamePlayers = _currentHandConfig.Variant switch
        {
            PokerVariant.TexasHoldem => (_currentGame as HoldEmGame)?.Players,
            PokerVariant.Omaha => (_currentGame as OmahaGame)?.Players,
            PokerVariant.SevenCardStud => (_currentGame as SevenCardStudGame)?.Players,
            PokerVariant.FiveCardDraw => (_currentGame as FiveCardDrawGame)?.Players,
            PokerVariant.Baseball => (_currentGame as BaseballGame)?.Players,
            PokerVariant.KingsAndLows => (_currentGame as KingsAndLowsGame)?.Players,
            PokerVariant.FollowTheQueen => (_currentGame as FollowTheQueenGame)?.Players,
            _ => null
        };

        if (gamePlayers != null)
        {
            foreach (var subGamePlayer in gamePlayers)
            {
                var mainPlayer = _gamePlayers.FirstOrDefault(gp => gp.Player.Name == subGamePlayer.Name);
                if (mainPlayer != null)
                {
                    // Sync chip stacks - the sub-game has the authoritative value
                    var delta = subGamePlayer.ChipStack - mainPlayer.Player.ChipStack;
                    if (delta > 0)
                    {
                        mainPlayer.Player.AddChips(delta);
                    }
                    else if (delta < 0)
                    {
                        mainPlayer.Player.PlaceBet(-delta);
                    }
                }
            }
        }
    }

    private void MoveDealer()
    {
        do
        {
            _dealerPosition = (_dealerPosition + 1) % _gamePlayers.Count;
        } while (_gamePlayers[_dealerPosition].Player.ChipStack <= 0 && GetPlayersWithChips().Count() >= 2);
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
    /// Gets the wild cards that would be designated as wild based on current configuration.
    /// </summary>
    public IReadOnlyCollection<int> GetWildCardValues()
    {
        if (_currentHandConfig?.WildCards == null || !_currentHandConfig.WildCards.Enabled)
        {
            return [];
        }

        return _currentHandConfig.WildCards.Type switch
        {
            WildCardType.DeucesWild => [2],
            WildCardType.ThreesAndNines => [3, 9],
            WildCardType.KingsWild => [13],
            WildCardType.QueensWild => [12],
            WildCardType.OneEyedJacks => [11], // Note: actual one-eyed jacks are Jh and Js
            WildCardType.JacksWild => [11],
            WildCardType.Custom => _currentHandConfig.WildCards.CustomWildValues,
            _ => []
        };
    }
}

/// <summary>
/// Result of selecting a variant.
/// </summary>
public class SelectVariantResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public PokerVariant SelectedVariant { get; init; }
    public bool RequiresWildCardConfig { get; init; }
}

/// <summary>
/// Result of configuring wild cards.
/// </summary>
public class ConfigureWildCardsResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public WildCardConfiguration? Configuration { get; init; }
}
