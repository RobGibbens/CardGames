using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayManualStartResyncTests
{
	[Theory]
	[InlineData(true, false, "HubSnapshot")]
	[InlineData(false, true, "HubReconnect")]
	[InlineData(false, false, "HttpReload")]
	public void ResolvePostStartResyncTarget_SelectsExpectedRecoveryPath(
		bool wasConnected,
		bool isConnectedAfterReconnectAttempt,
		string expectedTarget)
	{
		var method = typeof(TablePlay).GetMethod(
			"ResolvePostStartResyncTarget",
			BindingFlags.Static | BindingFlags.NonPublic);

		method.Should().NotBeNull("TablePlay should decide how to recover the caller view after a successful manual start");

		var result = method!.Invoke(null, [wasConnected, isConnectedAfterReconnectAttempt]);

		result.Should().NotBeNull();
		result!.ToString().Should().Be(expectedTarget);
	}
}
