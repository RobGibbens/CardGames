using System;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class ActionTimerUpdatePolicyTests
{
	[Fact]
	public void ShouldIgnore_DisplayedHand11_IgnoresHand10()
	{
		var incoming = new ActionTimerStateDto
		{
			HandNumber = 10,
			IsActive = true,
			SecondsRemaining = 0
		};

		ActionTimerUpdatePolicy.ShouldIgnore(incoming, displayedHandNumber: 11, appliedStateVersion: 0, displayedTimer: null)
			.Should().BeTrue();
	}

	[Fact]
	public void ShouldIgnore_OlderStateVersion_IsIgnoredOnlyWhenAppliedVersionIsPositive()
	{
		var incoming = new ActionTimerStateDto
		{
			HandNumber = 11,
			StateVersion = 19
		};

		ActionTimerUpdatePolicy.ShouldIgnore(incoming, displayedHandNumber: 11, appliedStateVersion: 20, displayedTimer: null)
			.Should().BeTrue();
		ActionTimerUpdatePolicy.ShouldIgnore(incoming, displayedHandNumber: 11, appliedStateVersion: 0, displayedTimer: null)
			.Should().BeFalse();
	}

	[Fact]
	public void ShouldIgnore_ActiveDisplayedTimer_IgnoresEarlierStartedAtUtc()
	{
		var later = new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);
		var earlier = later.AddSeconds(-30);
		var displayed = new ActionTimerStateDto
		{
			IsActive = true,
			StartedAtUtc = later,
			HandNumber = 11
		};

		ActionTimerUpdatePolicy.ShouldIgnore(
				new ActionTimerStateDto { IsActive = true, StartedAtUtc = earlier, HandNumber = 11 },
				11, 0, displayed)
			.Should().BeTrue();
		ActionTimerUpdatePolicy.ShouldIgnore(
				new ActionTimerStateDto { IsActive = false, StartedAtUtc = earlier, HandNumber = 11 },
				11, 0, displayed)
			.Should().BeTrue();
	}

	[Fact]
	public void ShouldIgnore_SameOrLaterStartedAtUtc_IsNotIgnored()
	{
		var startedAt = new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);
		var displayed = new ActionTimerStateDto
		{
			IsActive = true,
			StartedAtUtc = startedAt,
			HandNumber = 11
		};

		ActionTimerUpdatePolicy.ShouldIgnore(
				new ActionTimerStateDto { IsActive = false, StartedAtUtc = startedAt, HandNumber = 11, SecondsRemaining = 0 },
				11, 0, displayed)
			.Should().BeFalse();
		ActionTimerUpdatePolicy.ShouldIgnore(
				new ActionTimerStateDto { IsActive = true, StartedAtUtc = startedAt.AddSeconds(1), HandNumber = 11 },
				11, 0, displayed)
			.Should().BeFalse();
	}

	[Fact]
	public void ShouldIgnore_HandNumberZero_IsNotIgnoredByTheHandCheck()
	{
		var incoming = new ActionTimerStateDto
		{
			HandNumber = 0,
			IsActive = true,
			SecondsRemaining = 0
		};

		ActionTimerUpdatePolicy.ShouldIgnore(incoming, displayedHandNumber: 11, appliedStateVersion: 0, displayedTimer: null)
			.Should().BeFalse();
	}
}
