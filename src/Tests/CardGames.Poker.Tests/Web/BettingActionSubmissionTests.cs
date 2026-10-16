using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class BettingActionSubmissionTests
{
	[Fact]
	public void TryBegin_LocksUntilTheRequestIsRejected()
	{
		var submission = new BettingActionSubmission();

		submission.TryBegin(10).Should().BeTrue();
		submission.IsLocked.Should().BeTrue();
		submission.TryBegin(10).Should().BeFalse();

		submission.Reject();

		submission.IsLocked.Should().BeFalse();
		submission.TryBegin(10).Should().BeTrue();
	}

	[Fact]
	public void Accept_StaysLockedWhileTheSameDecisionIsStillShowing()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);

		submission.Accept(nextPlayerIndex: 2);
		submission.Observe(10, actionPanelVisible: true, currentActorSeatIndex: 0);
		submission.Observe(11, actionPanelVisible: true, currentActorSeatIndex: 0);

		submission.IsLocked.Should().BeTrue();
		submission.TryBegin(11).Should().BeFalse();
	}

	[Fact]
	public void Accept_UnlocksWhenThePanelIsGone()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);
		submission.Accept(nextPlayerIndex: 2);

		submission.Observe(10, actionPanelVisible: false, currentActorSeatIndex: 2);

		submission.IsLocked.Should().BeFalse();
	}

	[Fact]
	public void Accept_UnlocksWhenANewerSnapshotShowsTheNextPlayer()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);
		submission.Accept(nextPlayerIndex: 0);

		submission.Observe(11, actionPanelVisible: true, currentActorSeatIndex: 0);

		submission.IsLocked.Should().BeFalse();
	}

	[Fact]
	public void Accept_StaysLockedWhenTheRoundEndedAndThePanelIsStillVisible()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);
		submission.Accept(nextPlayerIndex: -1);

		submission.Observe(11, actionPanelVisible: true, currentActorSeatIndex: -1);

		submission.IsLocked.Should().BeTrue();
	}

	[Fact]
	public void Observe_DoesNotUnlockARequestThatHasNotBeenAccepted()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);

		submission.Observe(11, actionPanelVisible: false, currentActorSeatIndex: 1);
		submission.Observe(12, actionPanelVisible: true, currentActorSeatIndex: 0);

		submission.IsLocked.Should().BeTrue();
		submission.TryBegin(12).Should().BeFalse();
	}

	[Fact]
	public void Reject_AfterAccept_DoesNotClearAnAcceptedAction()
	{
		var submission = new BettingActionSubmission();
		submission.TryBegin(10);
		submission.Accept(nextPlayerIndex: 1);

		submission.Reject();

		submission.IsLocked.Should().BeTrue();
	}
}
