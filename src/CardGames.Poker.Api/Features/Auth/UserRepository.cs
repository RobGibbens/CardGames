using System.Security.Cryptography;

namespace CardGames.Poker.Api.Features.Auth;

public record UserRecord(
    string Id,
    string Email,
    string PasswordHash,
    string? DisplayName = null,
    string? AuthProvider = null,
    DateTime CreatedAt = default
);

public interface IUserRepository
{
    Task<UserRecord?> GetByEmailAsync(string email);
    Task<UserRecord?> GetByIdAsync(string id);
    Task<UserRecord> CreateAsync(string email, string passwordHash, string? displayName = null);
    Task<bool> EmailExistsAsync(string email);
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

    public Task<UserRecord> CreateAsync(string email, string passwordHash, string? displayName = null)
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
                CreatedAt: DateTime.UtcNow
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

    private static string GenerateSecureId()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
