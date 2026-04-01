using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services.Cache;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

/// <summary>
/// Minimal <see cref="IActiveGameCache"/> implementation for tests.
/// Always reports at least one cached game so adaptive polling never skips processing.
/// </summary>
public sealed class FakeActiveGameCache : IActiveGameCache
{
    public int Count => 1;

    public bool TryGet(Guid gameId, out CachedGameSnapshot snapshot)
    {
        snapshot = default!;
        return false;
    }

    public void Set(CachedGameSnapshot snapshot) { }
    public void UpsertPrivateState(Guid gameId, string userId, PrivateStateDto privateState, ulong versionNumber) { }
    public bool Evict(Guid gameId) => false;
    public int Compact(DateTimeOffset olderThanUtc) => 0;
    public IReadOnlyCollection<Guid> GetActiveGameIds() => Array.Empty<Guid>();
}
