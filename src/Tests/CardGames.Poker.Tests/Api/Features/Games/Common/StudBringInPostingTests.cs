using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common;

public class StudBringInPostingTests
{
	[Fact]
	public void FirstActorAfterBringIn_PostedHeadsUp_ActsFromTheOtherSeat()
	{
		var players = new[]
		{
			Player(0),
			Player(1)
		};

		var actor = StudBringInPosting.FirstActorAfterBringIn(players, bringInSeat: 1, amountPosted: 5);

		actor.ActorSeat.Should().Be(0);
		actor.LastAggressorIndex.Should().Be(1);
		actor.PlayersActed.Should().Be(1);
		actor.RoundComplete.Should().BeFalse();
	}

	[Fact]
	public void FirstActorAfterBringIn_PostedThreeHanded_ActsFromTheNextSeat()
	{
		var players = new[]
		{
			Player(0),
			Player(1),
			Player(2)
		};

		var actor = StudBringInPosting.FirstActorAfterBringIn(players, bringInSeat: 0, amountPosted: 5);

		actor.ActorSeat.Should().Be(1);
		actor.LastAggressorIndex.Should().Be(0);
		actor.PlayersActed.Should().Be(1);
		actor.RoundComplete.Should().BeFalse();
	}

	[Fact]
	public void FirstActorAfterBringIn_PostedAllIn_DoesNotCountThePoster()
	{
		var players = new[]
		{
			Player(0, allIn: true),
			Player(1),
			Player(2)
		};

		var actor = StudBringInPosting.FirstActorAfterBringIn(players, bringInSeat: 0, amountPosted: 5);

		actor.ActorSeat.Should().Be(1);
		actor.LastAggressorIndex.Should().Be(0);
		actor.PlayersActed.Should().Be(0);
		actor.RoundComplete.Should().BeFalse();
	}

	[Fact]
	public void FirstActorAfterBringIn_NoOtherLiveSeat_CompletesTheRound()
	{
		var players = new[]
		{
			Player(0),
			Player(1, folded: true),
			Player(2, allIn: true)
		};

		var actor = StudBringInPosting.FirstActorAfterBringIn(players, bringInSeat: 0, amountPosted: 5);

		actor.ActorSeat.Should().Be(-1);
		actor.LastAggressorIndex.Should().Be(0);
		actor.RoundComplete.Should().BeTrue();
	}

	[Fact]
	public void FirstActorAfterBringIn_NothingPosted_LeavesThePosterToOpen()
	{
		var players = new[]
		{
			Player(0),
			Player(1)
		};

		var actor = StudBringInPosting.FirstActorAfterBringIn(players, bringInSeat: 1, amountPosted: 0);

		actor.ActorSeat.Should().Be(1);
		actor.LastAggressorIndex.Should().Be(-1);
		actor.PlayersActed.Should().Be(0);
		actor.RoundComplete.Should().BeFalse();
	}

	private static GamePlayer Player(int seat, bool folded = false, bool allIn = false)
	{
		return new GamePlayer
		{
			SeatPosition = seat,
			HasFolded = folded,
			IsAllIn = allIn
		};
	}
}
