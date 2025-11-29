using System.Security.Cryptography;

namespace CardGames.Poker.Api.Features.History;

public record GameHistoryRecord(
    string Id,
    string UserId,
    string GameType,
    DateTime StartedAt,
    DateTime EndedAt,
    bool Won,
    long ChipsWon,
    long ChipsLost,
    int PlayerCount,
    string? TableName = null,
    string? TournamentName = null,
    string? HandSummary = null
);

public interface IHistoryRepository
{
    Task<GameHistoryRecord> AddGameAsync(GameHistoryRecord record);
    Task<IReadOnlyList<GameHistoryRecord>> GetPlayerHistoryAsync(string userId, int page = 1, int pageSize = 20);
    Task<int> GetPlayerGameCountAsync(string userId);
    Task<GameHistoryRecord?> GetByIdAsync(string id);
    Task<GameHistoryRecord?> GetByIdForUserAsync(string id, string userId);
}

public class InMemoryHistoryRepository : IHistoryRepository
{
    private const int SecureIdByteLength = 16;
    private readonly List<GameHistoryRecord> _history = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public Task<GameHistoryRecord> AddGameAsync(GameHistoryRecord record)
    {
        _lock.EnterWriteLock();
        try
        {
            var newRecord = record with { Id = GenerateSecureId() };
            _history.Add(newRecord);
            return Task.FromResult(newRecord);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<IReadOnlyList<GameHistoryRecord>> GetPlayerHistoryAsync(string userId, int page = 1, int pageSize = 20)
    {
        _lock.EnterReadLock();
        try
        {
            var history = _history
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.EndedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<GameHistoryRecord>>(history);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<int> GetPlayerGameCountAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var count = _history.Count(h => h.UserId == userId);
            return Task.FromResult(count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<GameHistoryRecord?> GetByIdAsync(string id)
    {
        _lock.EnterReadLock();
        try
        {
            var record = _history.FirstOrDefault(h => h.Id == id);
            return Task.FromResult(record);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<GameHistoryRecord?> GetByIdForUserAsync(string id, string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var record = _history.FirstOrDefault(h => h.Id == id && h.UserId == userId);
            return Task.FromResult(record);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static string GenerateSecureId()
    {
        var bytes = new byte[SecureIdByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
