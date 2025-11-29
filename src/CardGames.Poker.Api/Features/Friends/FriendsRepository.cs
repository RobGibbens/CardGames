using System.Security.Cryptography;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Friends;

public record FriendshipRecord(
    string Id,
    string SenderId,
    string ReceiverId,
    FriendshipStatus Status,
    DateTime CreatedAt,
    DateTime? RespondedAt = null
);

public interface IFriendsRepository
{
    Task<FriendshipRecord?> GetByIdAsync(string id);
    Task<IReadOnlyList<FriendshipRecord>> GetFriendshipsAsync(string userId);
    Task<IReadOnlyList<FriendshipRecord>> GetPendingInvitationsReceivedAsync(string userId);
    Task<IReadOnlyList<FriendshipRecord>> GetPendingInvitationsSentAsync(string userId);
    Task<FriendshipRecord?> GetExistingFriendshipAsync(string userId1, string userId2);
    Task<FriendshipRecord> CreateInvitationAsync(string senderId, string receiverId);
    Task<FriendshipRecord?> AcceptInvitationAsync(string invitationId, string userId);
    Task<FriendshipRecord?> RejectInvitationAsync(string invitationId, string userId);
    Task<bool> RemoveFriendAsync(string userId, string friendId);
}

public class InMemoryFriendsRepository : IFriendsRepository
{
    private const int SecureIdByteLength = 16;
    private readonly List<FriendshipRecord> _friendships = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public Task<FriendshipRecord?> GetByIdAsync(string id)
    {
        _lock.EnterReadLock();
        try
        {
            var record = _friendships.FirstOrDefault(f => f.Id == id);
            return Task.FromResult(record);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<FriendshipRecord>> GetFriendshipsAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var friendships = _friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.SenderId == userId || f.ReceiverId == userId))
                .ToList();
            return Task.FromResult<IReadOnlyList<FriendshipRecord>>(friendships);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<FriendshipRecord>> GetPendingInvitationsReceivedAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var invitations = _friendships
                .Where(f => f.Status == FriendshipStatus.Pending && f.ReceiverId == userId)
                .ToList();
            return Task.FromResult<IReadOnlyList<FriendshipRecord>>(invitations);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<FriendshipRecord>> GetPendingInvitationsSentAsync(string userId)
    {
        _lock.EnterReadLock();
        try
        {
            var invitations = _friendships
                .Where(f => f.Status == FriendshipStatus.Pending && f.SenderId == userId)
                .ToList();
            return Task.FromResult<IReadOnlyList<FriendshipRecord>>(invitations);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<FriendshipRecord?> GetExistingFriendshipAsync(string userId1, string userId2)
    {
        _lock.EnterReadLock();
        try
        {
            var existing = _friendships.FirstOrDefault(f =>
                (f.SenderId == userId1 && f.ReceiverId == userId2) ||
                (f.SenderId == userId2 && f.ReceiverId == userId1));
            return Task.FromResult(existing);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<FriendshipRecord> CreateInvitationAsync(string senderId, string receiverId)
    {
        _lock.EnterWriteLock();
        try
        {
            var record = new FriendshipRecord(
                Id: GenerateSecureId(),
                SenderId: senderId,
                ReceiverId: receiverId,
                Status: FriendshipStatus.Pending,
                CreatedAt: DateTime.UtcNow
            );
            _friendships.Add(record);
            return Task.FromResult(record);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<FriendshipRecord?> AcceptInvitationAsync(string invitationId, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _friendships.FindIndex(f =>
                f.Id == invitationId &&
                f.ReceiverId == userId &&
                f.Status == FriendshipStatus.Pending);

            if (index < 0)
            {
                return Task.FromResult<FriendshipRecord?>(null);
            }

            var updated = _friendships[index] with
            {
                Status = FriendshipStatus.Accepted,
                RespondedAt = DateTime.UtcNow
            };
            _friendships[index] = updated;
            return Task.FromResult<FriendshipRecord?>(updated);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<FriendshipRecord?> RejectInvitationAsync(string invitationId, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _friendships.FindIndex(f =>
                f.Id == invitationId &&
                f.ReceiverId == userId &&
                f.Status == FriendshipStatus.Pending);

            if (index < 0)
            {
                return Task.FromResult<FriendshipRecord?>(null);
            }

            var updated = _friendships[index] with
            {
                Status = FriendshipStatus.Rejected,
                RespondedAt = DateTime.UtcNow
            };
            _friendships[index] = updated;
            return Task.FromResult<FriendshipRecord?>(updated);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<bool> RemoveFriendAsync(string userId, string friendId)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _friendships.FindIndex(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.SenderId == userId && f.ReceiverId == friendId) ||
                 (f.SenderId == friendId && f.ReceiverId == userId)));

            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _friendships.RemoveAt(index);
            return Task.FromResult(true);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static string GenerateSecureId()
    {
        var bytes = new byte[SecureIdByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
