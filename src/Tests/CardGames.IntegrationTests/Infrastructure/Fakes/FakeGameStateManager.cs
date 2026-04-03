using CardGames.Poker.Api.Services.InMemoryEngine;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

/// <summary>
/// No-op implementation of <see cref="IGameStateManager"/> for integration tests.
/// The in-memory engine is disabled in tests, so all methods are stubs.
/// </summary>
public sealed class FakeGameStateManager : IGameStateManager
{
    public bool TryGetGame(Guid gameId, out ActiveGameRuntimeState state)
    {
        state = default!;
        return false;
    }

    public Task<ActiveGameRuntimeState?> GetOrLoadGameAsync(Guid gameId, CancellationToken cancellationToken)
        => Task.FromResult<ActiveGameRuntimeState?>(null);

    public Task<ActiveGameRuntimeState?> ReloadGameAsync(Guid gameId, CancellationToken cancellationToken)
        => Task.FromResult<ActiveGameRuntimeState?>(null);

    public void SetGame(ActiveGameRuntimeState state) { }

    public bool RemoveGame(Guid gameId) => false;

    public IReadOnlyCollection<Guid> GetActiveGameIds() => [];

    public int Count => 0;
}
