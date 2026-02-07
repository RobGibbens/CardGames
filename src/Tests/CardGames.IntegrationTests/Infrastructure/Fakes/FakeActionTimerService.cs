using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeActionTimerService : IActionTimerService
{
    public void StartTimer(Guid gameId, int playerSeatIndex, int durationSeconds = 60, Func<Guid, int, Task>? onExpired = null)
    {
        // No-op for tests
    }

    public void StartChipCheckPauseTimer(Guid gameId, int durationSeconds = 120, Func<Guid, Task>? onExpired = null)
    {
        // No-op for tests
    }

    public void StopTimer(Guid gameId)
    {
        // No-op for tests
    }

    public ActionTimerState? GetTimerState(Guid gameId)
    {
        return null;
    }

    public bool IsTimerActive(Guid gameId)
    {
        return false;
    }
}
