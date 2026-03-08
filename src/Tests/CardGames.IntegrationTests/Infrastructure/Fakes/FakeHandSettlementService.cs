using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeHandSettlementService : IHandSettlementService
{
    public List<(Game Game, Dictionary<string, int> Payouts)> SettledHands { get; } = new();

    public Task SettleHandAsync(Game game, Dictionary<string, int> payouts, CancellationToken cancellationToken)
    {
        SettledHands.Add((game, payouts));
        return Task.CompletedTask;
    }
}
