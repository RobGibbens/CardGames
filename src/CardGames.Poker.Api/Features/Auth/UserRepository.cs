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
    private readonly object _lock = new();

    public Task<UserRecord?> GetByEmailAsync(string email)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }
    }

    public Task<UserRecord?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            return Task.FromResult(user);
        }
    }

    public Task<UserRecord> CreateAsync(string email, string passwordHash, string? displayName = null)
    {
        lock (_lock)
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
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        lock (_lock)
        {
            var exists = _users.Any(u => 
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }
    }

    private static string GenerateSecureId()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
