using System;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class ActionTimerStoppedDtoTests
{
	[Fact]
	public void ToStoppedDto_ExpiredPlayerAction_KeepsSeatStartAndHand()
	{
		var startedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var state = new ActionTimerState
		{
			GameId = Guid.NewGuid(),
			PlayerSeatIndex = 2,
			DurationSeconds = 30,
			StartedAtUtc = startedAt,
			HandNumber = 4,
			StateVersion = 8
		};

		state.SecondsRemaining.Should().Be(0);

		var dto = ActionTimerService.ToStoppedDto(state);

		dto.IsActive.Should().BeFalse();
		dto.SecondsRemaining.Should().Be(0);
		dto.PlayerSeatIndex.Should().Be(2);
		dto.StartedAtUtc.Should().Be(startedAt);
		dto.HandNumber.Should().Be(4);
		dto.DurationSeconds.Should().Be(30);
		dto.StateVersion.Should().Be(8);
	}
}
