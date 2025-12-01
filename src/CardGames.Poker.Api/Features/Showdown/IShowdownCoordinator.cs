using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Showdown;

/// <summary>
/// Represents a player's state during showdown.
/// </summary>
public class ShowdownPlayerState
{
    public required string PlayerName { get; init; }
    public required IReadOnlyList<CardDto> HoleCards { get; init; }
    public HandDto? Hand { get; set; }
    public ShowdownRevealStatus Status { get; set; } = ShowdownRevealStatus.Pending;
    public int? RevealOrder { get; set; }
    public bool WasForcedReveal { get; set; }
    public bool IsEligibleForPot { get; set; }
    public bool HasFolded { get; init; }
    public bool IsAllIn { get; init; }
    public int TotalBetAmount { get; init; }
}

/// <summary>
/// Represents the context of a showdown in progress.
/// </summary>
public class ShowdownContext
{
    public Guid ShowdownId { get; } = Guid.NewGuid();
    public required Guid GameId { get; init; }
    public required int HandNumber { get; init; }
    public required ShowdownRulesDto ShowdownRules { get; init; }
    public required List<ShowdownPlayerState> Players { get; init; }
    public required string? LastAggressor { get; init; }
    public required bool HadAllInAction { get; init; }
    public required int DealerPosition { get; init; }
    public IReadOnlyList<CardDto>? CommunityCards { get; init; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public int CurrentRevealOrder { get; set; } = 0;
    public bool IsComplete { get; set; }
}

/// <summary>
/// Coordinates showdown logic for poker games, enforcing variant-specific rules.
/// </summary>
public interface IShowdownCoordinator
{
    /// <summary>
    /// Initializes a new showdown with the given context.
    /// </summary>
    ShowdownContext InitializeShowdown(
        Guid gameId,
        int handNumber,
        ShowdownRulesDto rules,
        IEnumerable<ShowdownPlayerState> players,
        string? lastAggressor,
        bool hadAllInAction,
        int dealerPosition,
        IReadOnlyList<CardDto>? communityCards = null);

    /// <summary>
    /// Gets the next player who should reveal their cards.
    /// </summary>
    string? GetNextToReveal(ShowdownContext context);

    /// <summary>
    /// Determines if a player can muck (not show) their cards.
    /// </summary>
    bool CanPlayerMuck(ShowdownContext context, string playerName);

    /// <summary>
    /// Determines if a player is forced to reveal their cards.
    /// </summary>
    bool MustPlayerReveal(ShowdownContext context, string playerName);

    /// <summary>
    /// Processes a player's decision to show their cards.
    /// </summary>
    ShowdownRevealResult ProcessReveal(ShowdownContext context, string playerName, HandDto hand);

    /// <summary>
    /// Processes a player's decision to muck their cards.
    /// </summary>
    ShowdownRevealResult ProcessMuck(ShowdownContext context, string playerName);

    /// <summary>
    /// Gets the current state of the showdown.
    /// </summary>
    ShowdownStateDto GetShowdownState(ShowdownContext context);

    /// <summary>
    /// Determines the winners of the showdown.
    /// </summary>
    IReadOnlyList<string> DetermineWinners(ShowdownContext context);

    /// <summary>
    /// Checks if the showdown is complete.
    /// </summary>
    bool IsShowdownComplete(ShowdownContext context);

    /// <summary>
    /// Gets the current best revealed hand (for determining if losers can muck).
    /// </summary>
    HandDto? GetCurrentBestHand(ShowdownContext context);

    /// <summary>
    /// Automatically reveals the winner's cards (when they've won and must show).
    /// </summary>
    ShowdownRevealResult AutoRevealWinner(ShowdownContext context, string playerName, HandDto hand);

    /// <summary>
    /// Processes an all-in showdown where all remaining community cards must be dealt.
    /// Returns the cards that need to be dealt.
    /// </summary>
    AllInShowdownResult ProcessAllInShowdown(ShowdownContext context, int totalCommunityCardsNeeded);

    /// <summary>
    /// Determines all winners with pot distribution for side pots.
    /// </summary>
    IReadOnlyList<WinnerDetermination> DetermineWinnersWithPots(
        ShowdownContext context,
        IReadOnlyList<PotInfo> pots);

    /// <summary>
    /// Builds the winner announcement with all details.
    /// </summary>
    WinnerAnnouncementDto BuildWinnerAnnouncement(
        ShowdownContext context,
        IReadOnlyList<WinnerDetermination> winners,
        bool wonByFold);

    /// <summary>
    /// Builds an animation sequence for the showdown.
    /// </summary>
    ShowdownAnimationSequenceDto BuildAnimationSequence(
        ShowdownContext context,
        IReadOnlyList<WinnerDetermination> winners);
}

/// <summary>
/// Result of a reveal or muck action during showdown.
/// </summary>
public record ShowdownRevealResult(
    bool Success,
    string? ErrorMessage,
    ShowdownRevealStatus NewStatus,
    string? NextToReveal,
    bool ShowdownComplete);

/// <summary>
/// Result of an all-in showdown processing.
/// </summary>
public record AllInShowdownResult(
    bool Success,
    string? ErrorMessage,
    int CommunityCardsNeeded,
    IReadOnlyList<string> PlayersToAutoReveal);

/// <summary>
/// Represents information about a pot for winner determination.
/// </summary>
public record PotInfo(
    int PotIndex,
    int Amount,
    IReadOnlyList<string> EligiblePlayers,
    bool IsMainPot);

/// <summary>
/// Represents a winner determination result including pot share.
/// </summary>
public record WinnerDetermination(
    string PlayerName,
    HandDto? Hand,
    IReadOnlyList<CardDto>? WinningCards,
    IReadOnlyList<CardDto>? HoleCards,
    int AmountWon,
    int PotIndex,
    bool IsTie);
