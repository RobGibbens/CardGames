using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Showdown;

/// <summary>
/// Default implementation of the showdown coordinator.
/// Enforces showdown rules for each poker variant including:
/// - Who reveals first (last aggressor, clockwise from button, etc.)
/// - Muck handling (when allowed, when forced to show)
/// - Privacy rules for folded cards
/// </summary>
public class ShowdownCoordinator : IShowdownCoordinator
{
    private readonly ILogger<ShowdownCoordinator>? _logger;

    public ShowdownCoordinator(ILogger<ShowdownCoordinator>? logger = null)
    {
        _logger = logger;
    }

    public ShowdownContext InitializeShowdown(
        Guid gameId,
        int handNumber,
        ShowdownRulesDto rules,
        IEnumerable<ShowdownPlayerState> players,
        string? lastAggressor,
        bool hadAllInAction,
        int dealerPosition,
        IReadOnlyList<CardDto>? communityCards = null)
    {
        var playerList = players.ToList();

        // Mark folded players as such
        foreach (var player in playerList.Where(p => p.HasFolded))
        {
            player.Status = ShowdownRevealStatus.Folded;
        }

        var context = new ShowdownContext
        {
            GameId = gameId,
            HandNumber = handNumber,
            ShowdownRules = rules,
            Players = playerList,
            LastAggressor = lastAggressor,
            HadAllInAction = hadAllInAction,
            DealerPosition = dealerPosition,
            CommunityCards = communityCards
        };

        _logger?.LogInformation(
            "Showdown initialized for game {GameId}, hand {HandNumber}. Players: {PlayerCount}, LastAggressor: {LastAggressor}, AllInAction: {AllInAction}",
            gameId, handNumber, playerList.Count(p => !p.HasFolded), lastAggressor, hadAllInAction);

        return context;
    }

    public string? GetNextToReveal(ShowdownContext context)
    {
        var eligiblePlayers = context.Players
            .Where(p => !p.HasFolded && p.Status == ShowdownRevealStatus.Pending)
            .ToList();

        if (eligiblePlayers.Count == 0)
        {
            return null;
        }

        return context.ShowdownRules.ShowOrder switch
        {
            ShowdownOrder.LastAggressor => GetNextByLastAggressor(context, eligiblePlayers),
            ShowdownOrder.ClockwiseFromButton => GetNextClockwise(context, eligiblePlayers),
            ShowdownOrder.CounterClockwiseFromButton => GetNextCounterClockwise(context, eligiblePlayers),
            ShowdownOrder.Simultaneous => eligiblePlayers.FirstOrDefault()?.PlayerName,
            _ => eligiblePlayers.FirstOrDefault()?.PlayerName
        };
    }

    private string? GetNextByLastAggressor(ShowdownContext context, List<ShowdownPlayerState> eligiblePlayers)
    {
        // If no reveals yet, start with last aggressor (if they're eligible)
        if (context.CurrentRevealOrder == 0)
        {
            var lastAggressor = eligiblePlayers.FirstOrDefault(p => p.PlayerName == context.LastAggressor);
            if (lastAggressor != null)
            {
                return lastAggressor.PlayerName;
            }
        }

        // Otherwise, continue clockwise from the last person who revealed
        return GetNextClockwise(context, eligiblePlayers);
    }

    private string? GetNextClockwise(ShowdownContext context, List<ShowdownPlayerState> eligiblePlayers)
    {
        // Find position of the last player to reveal, or start from dealer
        int startPosition;
        
        var lastRevealed = context.Players
            .Where(p => p.RevealOrder.HasValue)
            .OrderByDescending(p => p.RevealOrder)
            .FirstOrDefault();

        if (lastRevealed != null)
        {
            startPosition = context.Players.FindIndex(p => p.PlayerName == lastRevealed.PlayerName);
        }
        else if (context.LastAggressor != null && context.ShowdownRules.ShowOrder == ShowdownOrder.LastAggressor)
        {
            // When using LastAggressor order and no one has revealed yet,
            // we need to set up the starting position such that the clockwise search
            // (which starts at startPosition + 1) will find the last aggressor first.
            // Therefore, we set startPosition to be one position before the last aggressor.
            var lastAggressorPosition = context.Players.FindIndex(p => p.PlayerName == context.LastAggressor);
            if (lastAggressorPosition >= 0)
            {
                startPosition = (lastAggressorPosition - 1 + context.Players.Count) % context.Players.Count;
            }
            else
            {
                startPosition = context.DealerPosition;
            }
        }
        else
        {
            startPosition = context.DealerPosition;
        }

        // Find next eligible player clockwise
        for (int i = 1; i <= context.Players.Count; i++)
        {
            int pos = (startPosition + i) % context.Players.Count;
            var player = context.Players[pos];
            if (eligiblePlayers.Contains(player))
            {
                return player.PlayerName;
            }
        }

        return eligiblePlayers.FirstOrDefault()?.PlayerName;
    }

    private string? GetNextCounterClockwise(ShowdownContext context, List<ShowdownPlayerState> eligiblePlayers)
    {
        int startPosition;
        
        var lastRevealed = context.Players
            .Where(p => p.RevealOrder.HasValue)
            .OrderByDescending(p => p.RevealOrder)
            .FirstOrDefault();

        if (lastRevealed != null)
        {
            startPosition = context.Players.FindIndex(p => p.PlayerName == lastRevealed.PlayerName);
        }
        else
        {
            startPosition = context.DealerPosition;
        }

        // Find next eligible player counter-clockwise
        for (int i = 1; i <= context.Players.Count; i++)
        {
            int pos = (startPosition - i + context.Players.Count) % context.Players.Count;
            var player = context.Players[pos];
            if (eligiblePlayers.Contains(player))
            {
                return player.PlayerName;
            }
        }

        return eligiblePlayers.FirstOrDefault()?.PlayerName;
    }

    public bool CanPlayerMuck(ShowdownContext context, string playerName)
    {
        var player = context.Players.FirstOrDefault(p => p.PlayerName == playerName);
        if (player == null || player.HasFolded)
        {
            return false;
        }

        // If mucking is not allowed, player cannot muck
        if (!context.ShowdownRules.AllowMuck)
        {
            return false;
        }

        // If all-in action and rules require showing all on all-in, player cannot muck
        if (context.HadAllInAction && context.ShowdownRules.ShowAllOnAllIn)
        {
            return false;
        }

        // If player is first to reveal (last aggressor), they cannot muck
        if (context.CurrentRevealOrder == 0 && context.LastAggressor == playerName)
        {
            return false;
        }

        // If there's currently a best hand shown, player can muck if they can't beat it.
        // Note: At showdown time, the player hasn't evaluated their hand yet, so this check
        // only applies when re-evaluating muck eligibility during the showdown process.
        // In practice, players can muck if there's already a better hand shown.
        var bestHand = GetCurrentBestHand(context);
        if (bestHand != null)
        {
            // If a hand has been shown, any remaining player can choose to muck
            // since they know they either beat it (and should show) or don't (and can muck)
            return true;
        }

        // If player is all-in, they typically must show
        if (player.IsAllIn)
        {
            return false;
        }

        return true;
    }

    public bool MustPlayerReveal(ShowdownContext context, string playerName)
    {
        var player = context.Players.FirstOrDefault(p => p.PlayerName == playerName);
        if (player == null || player.HasFolded)
        {
            return false;
        }

        // If all-in action and rules require showing all on all-in
        if (context.HadAllInAction && context.ShowdownRules.ShowAllOnAllIn)
        {
            return true;
        }

        // If player is the last aggressor and is first to reveal
        if (context.CurrentRevealOrder == 0 && context.LastAggressor == playerName)
        {
            return true;
        }

        // If player is all-in
        if (player.IsAllIn)
        {
            return true;
        }

        // If no mucking is allowed
        if (!context.ShowdownRules.AllowMuck)
        {
            return true;
        }

        // If player is the only one remaining
        var remainingPlayers = context.Players
            .Count(p => !p.HasFolded && p.Status == ShowdownRevealStatus.Pending);
        if (remainingPlayers == 1)
        {
            // Check if there's already a shown hand
            var bestHand = GetCurrentBestHand(context);
            if (bestHand == null)
            {
                // Must show to claim pot
                return true;
            }
        }

        return false;
    }

    public ShowdownRevealResult ProcessReveal(ShowdownContext context, string playerName, HandDto hand)
    {
        var player = context.Players.FirstOrDefault(p => p.PlayerName == playerName);
        if (player == null)
        {
            return new ShowdownRevealResult(false, "Player not found", ShowdownRevealStatus.Pending, null, false);
        }

        if (player.HasFolded)
        {
            return new ShowdownRevealResult(false, "Folded players cannot reveal", ShowdownRevealStatus.Folded, null, false);
        }

        if (player.Status != ShowdownRevealStatus.Pending)
        {
            return new ShowdownRevealResult(false, "Player has already acted", player.Status, null, false);
        }

        // Verify it's this player's turn
        var nextToReveal = GetNextToReveal(context);
        if (nextToReveal != playerName && context.ShowdownRules.ShowOrder != ShowdownOrder.Simultaneous)
        {
            return new ShowdownRevealResult(false, $"Not this player's turn. Expected: {nextToReveal}", ShowdownRevealStatus.Pending, nextToReveal, false);
        }

        // Determine if this is a forced reveal BEFORE incrementing the reveal order
        // This ensures the last aggressor check (CurrentRevealOrder == 0) works correctly
        var wasForcedReveal = MustPlayerReveal(context, playerName);

        context.CurrentRevealOrder++;
        player.RevealOrder = context.CurrentRevealOrder;
        player.Hand = hand;
        player.WasForcedReveal = wasForcedReveal;
        player.Status = wasForcedReveal ? ShowdownRevealStatus.ForcedReveal : ShowdownRevealStatus.Shown;

        _logger?.LogInformation(
            "Player {PlayerName} revealed cards in showdown {ShowdownId}. Hand: {HandType}, Order: {RevealOrder}",
            playerName, context.ShowdownId, hand.HandType, player.RevealOrder);

        var showdownComplete = IsShowdownComplete(context);
        if (showdownComplete)
        {
            context.IsComplete = true;
        }

        return new ShowdownRevealResult(
            true,
            null,
            player.Status,
            GetNextToReveal(context),
            showdownComplete);
    }

    public ShowdownRevealResult ProcessMuck(ShowdownContext context, string playerName)
    {
        var player = context.Players.FirstOrDefault(p => p.PlayerName == playerName);
        if (player == null)
        {
            return new ShowdownRevealResult(false, "Player not found", ShowdownRevealStatus.Pending, null, false);
        }

        if (player.HasFolded)
        {
            return new ShowdownRevealResult(false, "Folded players cannot muck", ShowdownRevealStatus.Folded, null, false);
        }

        if (player.Status != ShowdownRevealStatus.Pending)
        {
            return new ShowdownRevealResult(false, "Player has already acted", player.Status, null, false);
        }

        if (!CanPlayerMuck(context, playerName))
        {
            return new ShowdownRevealResult(false, "Player is not allowed to muck", ShowdownRevealStatus.Pending, null, false);
        }

        // Verify it's this player's turn
        var nextToReveal = GetNextToReveal(context);
        if (nextToReveal != playerName && context.ShowdownRules.ShowOrder != ShowdownOrder.Simultaneous)
        {
            return new ShowdownRevealResult(false, $"Not this player's turn. Expected: {nextToReveal}", ShowdownRevealStatus.Pending, nextToReveal, false);
        }

        player.Status = ShowdownRevealStatus.Mucked;
        player.IsEligibleForPot = false;

        _logger?.LogInformation(
            "Player {PlayerName} mucked cards in showdown {ShowdownId}",
            playerName, context.ShowdownId);

        var showdownComplete = IsShowdownComplete(context);
        if (showdownComplete)
        {
            context.IsComplete = true;
        }

        return new ShowdownRevealResult(
            true,
            null,
            ShowdownRevealStatus.Mucked,
            GetNextToReveal(context),
            showdownComplete);
    }

    public ShowdownStateDto GetShowdownState(ShowdownContext context)
    {
        var reveals = context.Players.Select(p => new Shared.DTOs.ShowdownRevealDto(
            p.PlayerName,
            p.Status,
            p.Status is ShowdownRevealStatus.Shown or ShowdownRevealStatus.ForcedReveal ? p.HoleCards : null,
            p.Hand,
            p.WasForcedReveal,
            p.RevealOrder,
            p.IsEligibleForPot && !p.HasFolded && p.Status != ShowdownRevealStatus.Mucked
        )).ToList();

        return new ShowdownStateDto(
            context.ShowdownId,
            context.GameId,
            context.HandNumber,
            context.ShowdownRules.ShowOrder,
            context.ShowdownRules.AllowMuck,
            context.ShowdownRules.ShowAllOnAllIn,
            context.LastAggressor,
            reveals,
            GetNextToReveal(context),
            context.HadAllInAction,
            context.IsComplete,
            context.CommunityCards,
            context.StartedAt);
    }

    public IReadOnlyList<string> DetermineWinners(ShowdownContext context)
    {
        var eligiblePlayers = context.Players
            .Where(p => !p.HasFolded && 
                        (p.Status is ShowdownRevealStatus.Shown or ShowdownRevealStatus.ForcedReveal) &&
                        p.Hand != null)
            .ToList();

        if (eligiblePlayers.Count == 0)
        {
            return [];
        }

        var maxStrength = eligiblePlayers.Max(p => p.Hand!.Strength);
        
        return eligiblePlayers
            .Where(p => p.Hand!.Strength == maxStrength)
            .Select(p => p.PlayerName)
            .ToList();
    }

    public bool IsShowdownComplete(ShowdownContext context)
    {
        // Showdown is complete when all eligible players have acted
        var pendingPlayers = context.Players
            .Count(p => !p.HasFolded && p.Status == ShowdownRevealStatus.Pending);

        return pendingPlayers == 0;
    }

    public HandDto? GetCurrentBestHand(ShowdownContext context)
    {
        var revealedHands = context.Players
            .Where(p => p.Status is ShowdownRevealStatus.Shown or ShowdownRevealStatus.ForcedReveal && p.Hand != null)
            .Select(p => p.Hand!)
            .ToList();

        if (revealedHands.Count == 0)
        {
            return null;
        }

        return revealedHands.OrderByDescending(h => h.Strength).First();
    }
}
