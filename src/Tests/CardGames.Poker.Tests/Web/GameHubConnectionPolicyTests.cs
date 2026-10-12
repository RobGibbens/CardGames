using CardGames.Poker.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class GameHubConnectionPolicyTests
{
	[Fact]
	public void ShouldBuildNewConnection_NullState_Builds()
	{
		GameHubConnectionPolicy.ShouldBuildNewConnection(null).Should().BeTrue();
	}

	[Fact]
	public void ShouldBuildNewConnection_Disconnected_Builds()
	{
		GameHubConnectionPolicy.ShouldBuildNewConnection(HubConnectionState.Disconnected).Should().BeTrue();
	}

	[Theory]
	[InlineData(HubConnectionState.Connected)]
	[InlineData(HubConnectionState.Connecting)]
	[InlineData(HubConnectionState.Reconnecting)]
	public void ShouldBuildNewConnection_LiveAttempt_DoesNotBuild(HubConnectionState state)
	{
		GameHubConnectionPolicy.ShouldBuildNewConnection(state).Should().BeFalse();
	}

	[Fact]
	public void IsCurrent_SameGeneration_IsCurrent()
	{
		GameHubConnectionPolicy.IsCurrent(2, 2).Should().BeTrue();
	}

	[Fact]
	public void IsCurrent_OlderGeneration_IsNotCurrent()
	{
		GameHubConnectionPolicy.IsCurrent(1, 2).Should().BeFalse();
	}
}
