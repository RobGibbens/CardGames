using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Api.Features.Showdown;

/// <summary>
/// Record for audit log entries related to showdown events.
/// </summary>
public record ShowdownAuditEntry(
    Guid ShowdownId,
    Guid GameId,
    DateTime Timestamp,
    string EventType,
    string? PlayerName,
    string Details,
    IReadOnlyDictionary<string, object>? Metadata = null);

/// <summary>
/// Interface for logging showdown-related events for audit purposes.
/// </summary>
public interface IShowdownAuditLogger
{
    /// <summary>
    /// Logs when a showdown begins.
    /// </summary>
    Task LogShowdownStartedAsync(ShowdownStartedEvent evt);

    /// <summary>
    /// Logs when a player reveals their cards.
    /// </summary>
    Task LogPlayerRevealedAsync(PlayerRevealedCardsEvent evt);

    /// <summary>
    /// Logs when a player mucks their cards.
    /// </summary>
    Task LogPlayerMuckedAsync(PlayerMuckedCardsEvent evt);

    /// <summary>
    /// Logs when showdown is complete.
    /// </summary>
    Task LogShowdownCompletedAsync(ShowdownCompletedEvent evt);

    /// <summary>
    /// Gets audit entries for a specific showdown.
    /// </summary>
    Task<IReadOnlyList<ShowdownAuditEntry>> GetShowdownAuditAsync(Guid showdownId);

    /// <summary>
    /// Gets audit entries for a specific game.
    /// </summary>
    Task<IReadOnlyList<ShowdownAuditEntry>> GetGameShowdownAuditsAsync(Guid gameId);
}

/// <summary>
/// In-memory implementation of showdown audit logger.
/// </summary>
public class InMemoryShowdownAuditLogger : IShowdownAuditLogger
{
    private readonly List<ShowdownAuditEntry> _entries = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<InMemoryShowdownAuditLogger>? _logger;

    public InMemoryShowdownAuditLogger(ILogger<InMemoryShowdownAuditLogger>? logger = null)
    {
        _logger = logger;
    }

    public Task LogShowdownStartedAsync(ShowdownStartedEvent evt)
    {
        var entry = new ShowdownAuditEntry(
            evt.ShowdownId,
            evt.GameId,
            evt.Timestamp,
            "ShowdownStarted",
            null,
            $"Showdown started for hand {evt.HandNumber} with {evt.EligiblePlayers.Count} eligible players",
            new Dictionary<string, object>
            {
                ["HandNumber"] = evt.HandNumber,
                ["EligiblePlayers"] = evt.EligiblePlayers,
                ["FirstToReveal"] = evt.FirstToReveal ?? "N/A",
                ["HadAllInAction"] = evt.HadAllInAction
            });

        AddEntry(entry);
        _logger?.LogInformation(
            "Showdown audit: Started - ShowdownId: {ShowdownId}, GameId: {GameId}, Hand: {HandNumber}",
            evt.ShowdownId, evt.GameId, evt.HandNumber);

        return Task.CompletedTask;
    }

    public Task LogPlayerRevealedAsync(PlayerRevealedCardsEvent evt)
    {
        var entry = new ShowdownAuditEntry(
            evt.ShowdownId,
            evt.GameId,
            evt.Timestamp,
            "PlayerRevealed",
            evt.PlayerName,
            $"Player {evt.PlayerName} revealed cards (order: {evt.RevealOrder}){(evt.WasForcedReveal ? " - forced reveal" : "")}",
            new Dictionary<string, object>
            {
                ["RevealOrder"] = evt.RevealOrder,
                ["WasForcedReveal"] = evt.WasForcedReveal,
                ["HandType"] = evt.Hand?.HandType ?? "Unknown",
                ["HandStrength"] = evt.Hand?.Strength ?? 0
            });

        AddEntry(entry);
        _logger?.LogInformation(
            "Showdown audit: Player revealed - ShowdownId: {ShowdownId}, Player: {PlayerName}, Hand: {HandType}",
            evt.ShowdownId, evt.PlayerName, evt.Hand?.HandType);

        return Task.CompletedTask;
    }

    public Task LogPlayerMuckedAsync(PlayerMuckedCardsEvent evt)
    {
        var entry = new ShowdownAuditEntry(
            evt.ShowdownId,
            evt.GameId,
            evt.Timestamp,
            "PlayerMucked",
            evt.PlayerName,
            $"Player {evt.PlayerName} mucked cards{(evt.WasAllowedToMuck ? " - allowed" : " - forced")}",
            new Dictionary<string, object>
            {
                ["WasAllowedToMuck"] = evt.WasAllowedToMuck
            });

        AddEntry(entry);
        _logger?.LogInformation(
            "Showdown audit: Player mucked - ShowdownId: {ShowdownId}, Player: {PlayerName}",
            evt.ShowdownId, evt.PlayerName);

        return Task.CompletedTask;
    }

    public Task LogShowdownCompletedAsync(ShowdownCompletedEvent evt)
    {
        var entry = new ShowdownAuditEntry(
            evt.ShowdownId,
            evt.GameId,
            evt.Timestamp,
            "ShowdownCompleted",
            null,
            $"Showdown completed for hand {evt.HandNumber}. Winners: {string.Join(", ", evt.Winners)}",
            new Dictionary<string, object>
            {
                ["HandNumber"] = evt.HandNumber,
                ["Winners"] = evt.Winners,
                ["Payouts"] = evt.Payouts,
                ["RevealCount"] = evt.FinalReveals.Count(r => r.Status == "Shown" || r.Status == "ForcedReveal")
            });

        AddEntry(entry);
        _logger?.LogInformation(
            "Showdown audit: Completed - ShowdownId: {ShowdownId}, Winners: {Winners}",
            evt.ShowdownId, string.Join(", ", evt.Winners));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShowdownAuditEntry>> GetShowdownAuditAsync(Guid showdownId)
    {
        _lock.EnterReadLock();
        try
        {
            var entries = _entries
                .Where(e => e.ShowdownId == showdownId)
                .OrderBy(e => e.Timestamp)
                .ToList();
            return Task.FromResult<IReadOnlyList<ShowdownAuditEntry>>(entries);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<ShowdownAuditEntry>> GetGameShowdownAuditsAsync(Guid gameId)
    {
        _lock.EnterReadLock();
        try
        {
            var entries = _entries
                .Where(e => e.GameId == gameId)
                .OrderBy(e => e.Timestamp)
                .ToList();
            return Task.FromResult<IReadOnlyList<ShowdownAuditEntry>>(entries);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void AddEntry(ShowdownAuditEntry entry)
    {
        _lock.EnterWriteLock();
        try
        {
            _entries.Add(entry);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
