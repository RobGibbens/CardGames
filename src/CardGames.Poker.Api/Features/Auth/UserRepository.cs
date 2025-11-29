using System.Security.Cryptography;

namespace CardGames.Poker.Api.Features.Auth;

public record UserRecord(
    string Id,
    string Email,
    string PasswordHash,
    string? DisplayName = null,
    string? AuthProvider = null,
    DateTime CreatedAt = default,
    long ChipBalance = 0,
    string? AvatarUrl = null,
    int GamesPlayed = 0,
    int GamesWon = 0,
    int GamesLost = 0,
    long TotalChipsWon = 0,
    long TotalChipsLost = 0
);

public interface IUserRepository
{
    Task<UserRecord?> GetByEmailAsync(string email);
    Task<UserRecord?> GetByIdAsync(string id);
    Task<UserRecord> CreateAsync(string email, string passwordHash, string? displayName = null, long initialChipBalance = 0);
    Task<bool> EmailExistsAsync(string email);
    Task<UserRecord?> UpdateChipBalanceAsync(string userId, long newBalance);
    Task<UserRecord?> AdjustChipBalanceAsync(string userId, long adjustment);
    Task<UserRecord?> UpdateProfileAsync(string userId, string? displayName = null, string? avatarUrl = null);
    Task<UserRecord?> RecordGameResultAsync(string userId, bool won, long chipsWon, long chipsLost);
}

public class InMemoryUserRepository : IUserRepository
{
    private readonly List<UserRecord> _users = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public Task<UserRecord?> GetByEmailAsync(string email)
    {
        _lock.EnterReadLock();
        try
        {
            var user = _users.FirstOrDefault(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<UserRecord?> GetByIdAsync(string id)
    {
        _lock.EnterReadLock();
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            return Task.FromResult(user);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<UserRecord> CreateAsync(string email, string passwordHash, string? displayName = null, long initialChipBalance = 0)
    {
        _lock.EnterWriteLock();
        try
        {
            var user = new UserRecord(
                Id: GenerateSecureId(),
                Email: email,
                PasswordHash: passwordHash,
                DisplayName: displayName,
                AuthProvider: "local",
                CreatedAt: DateTime.UtcNow,
                ChipBalance: initialChipBalance
            );
            _users.Add(user);
            return Task.FromResult(user);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        _lock.EnterReadLock();
        try
        {
            var exists = _users.Any(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<UserRecord?> UpdateChipBalanceAsync(string userId, long newBalance)
    {
        if (newBalance < 0)
        {
            throw new ArgumentException("Chip balance cannot be negative", nameof(newBalance));
        }

        _lock.EnterWriteLock();
        try
        {
            var index = _users.FindIndex(u => u.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserRecord?>(null);
            }

            var updatedUser = _users[index] with { ChipBalance = newBalance };
            _users[index] = updatedUser;
            return Task.FromResult<UserRecord?>(updatedUser);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<UserRecord?> AdjustChipBalanceAsync(string userId, long adjustment)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _users.FindIndex(u => u.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserRecord?>(null);
            }

            var currentUser = _users[index];
            var newBalance = currentUser.ChipBalance + adjustment;
            
            if (newBalance < 0)
            {
                throw new InvalidOperationException("Insufficient chip balance");
            }

            var updatedUser = currentUser with { ChipBalance = newBalance };
            _users[index] = updatedUser;
            return Task.FromResult<UserRecord?>(updatedUser);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<UserRecord?> UpdateProfileAsync(string userId, string? displayName = null, string? avatarUrl = null)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _users.FindIndex(u => u.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserRecord?>(null);
            }

            var currentUser = _users[index];
            var updatedUser = currentUser with
            {
                DisplayName = displayName ?? currentUser.DisplayName,
                AvatarUrl = avatarUrl ?? currentUser.AvatarUrl
            };
            _users[index] = updatedUser;
            return Task.FromResult<UserRecord?>(updatedUser);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<UserRecord?> RecordGameResultAsync(string userId, bool won, long chipsWon, long chipsLost)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _users.FindIndex(u => u.Id == userId);
            if (index < 0)
            {
                return Task.FromResult<UserRecord?>(null);
            }

            var currentUser = _users[index];
            var updatedUser = currentUser with
            {
                GamesPlayed = currentUser.GamesPlayed + 1,
                GamesWon = won ? currentUser.GamesWon + 1 : currentUser.GamesWon,
                GamesLost = won ? currentUser.GamesLost : currentUser.GamesLost + 1,
                TotalChipsWon = currentUser.TotalChipsWon + chipsWon,
                TotalChipsLost = currentUser.TotalChipsLost + chipsLost
            };
            _users[index] = updatedUser;
            return Task.FromResult<UserRecord?>(updatedUser);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static string GenerateSecureId()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
