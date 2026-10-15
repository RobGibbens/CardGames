using System;
using CardGames.Poker.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class LobbyTableDisplayTests
{
	[Fact]
	public void DealersChoiceWithoutVariant_ShowsDealersChoiceImage_AndOmitsEmptyParentheses()
	{
		var table = CreateTable(
			gameTypeName: "",
			gameTypeMetadataName: null,
			gameTypeImageName: null,
			isDealersChoice: true);

		table.LobbyImageName.Should().Be(GetActiveGamesResponse.DealersChoiceImageName);
		table.LobbyGameTypeName.Should().Be("Dealer's Choice");
	}

	[Fact]
	public void DealersChoiceWithBlankMetadata_DoesNotRenderEmptyParentheses()
	{
		var table = CreateTable(
			gameTypeName: "Dealer's Choice",
			gameTypeMetadataName: "",
			gameTypeImageName: "",
			isDealersChoice: true);

		table.LobbyImageName.Should().Be("dealerschoice.png");
		table.LobbyGameTypeName.Should().Be("Dealer's Choice");
	}

	[Fact]
	public void DealersChoiceWithChosenVariant_ShowsVariantImageAndName()
	{
		var table = CreateTable(
			gameTypeName: "Texas Hold 'Em",
			gameTypeMetadataName: "Texas Hold 'Em",
			gameTypeImageName: "holdem.png",
			isDealersChoice: true);

		table.LobbyImageName.Should().Be("holdem.png");
		table.LobbyGameTypeName.Should().Be("Dealer's Choice (Texas Hold 'Em)");
	}

	[Fact]
	public void StandardTable_UsesItsOwnImageAndName()
	{
		var table = CreateTable(
			gameTypeName: "Texas Hold 'Em",
			gameTypeMetadataName: "Texas Hold 'Em",
			gameTypeImageName: "holdem.png",
			isDealersChoice: false);

		table.LobbyImageName.Should().Be("holdem.png");
		table.LobbyGameTypeName.Should().Be("Texas Hold 'Em");
	}

	private static GetActiveGamesResponse CreateTable(
		string gameTypeName,
		string? gameTypeMetadataName,
		string? gameTypeImageName,
		bool isDealersChoice)
	{
		return new GetActiveGamesResponse(
			DateTimeOffset.UtcNow,
			"user",
			"Rob",
			"WaitingToStart",
			null!,
			null!,
			null!,
			Guid.Empty,
			gameTypeImageName!,
			gameTypeMetadataName!,
			gameTypeName,
			Guid.NewGuid(),
			"Test",
			"row",
			GameStatus.WaitingForPlayers)
		{
			IsDealersChoice = isDealersChoice
		};
	}
}
