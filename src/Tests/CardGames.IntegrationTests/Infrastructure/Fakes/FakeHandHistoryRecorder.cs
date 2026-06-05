using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeHandHistoryRecorder : IHandHistoryRecorder
{
	public List<RecordHandHistoryParameters> RecordedHands { get; } = [];

	public Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters,
		CancellationToken cancellationToken = default)
	{
		RecordedHands.Add(parameters);
		return Task.FromResult(true);
	}
}