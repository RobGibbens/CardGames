using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using FluentAssertions;
using Xunit;
using Layer = CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction.FiveCardDrawSidePots.Layer;
using Stake = CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction.FiveCardDrawSidePots.Stake;

namespace CardGames.Poker.Tests.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;

public class FiveCardDrawSidePotsTests
{
	private static readonly Guid Dave = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
	private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
	private static readonly Guid Bob = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
	private static readonly Guid Carol = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");

	[Fact]
	public void FoldedAnteBelowTheShortAllIn_StaysInTheMainLayer()
	{
		var stakes = new Stake[]
		{
			new(Dave, 10, HasFolded: true, IsAllIn: false),
			new(Alice, 50, HasFolded: false, IsAllIn: true),
			new(Bob, 150, HasFolded: false, IsAllIn: false),
			new(Carol, 150, HasFolded: false, IsAllIn: false)
		};

		var layers = FiveCardDrawSidePots.Build(stakes);

		layers.Should().HaveCount(2);
		AssertBalanced(layers, stakes);

		var main = layers[0];
		main.PotOrder.Should().Be(0);
		main.Amount.Should().Be(160);
		Slice(main, Dave).Amount.Should().Be(10);
		Slice(main, Dave).Eligible.Should().BeFalse();
		Slice(main, Alice).Amount.Should().Be(50);
		Slice(main, Alice).Eligible.Should().BeTrue();
		Slice(main, Bob).Amount.Should().Be(50);
		Slice(main, Bob).Eligible.Should().BeTrue();
		Slice(main, Carol).Amount.Should().Be(50);
		Slice(main, Carol).Eligible.Should().BeTrue();

		var side = layers[1];
		side.PotOrder.Should().Be(1);
		side.Amount.Should().Be(200);
		side.Slices.Select(slice => slice.GamePlayerId).Should().BeEquivalentTo([Bob, Carol]);
		Slice(side, Bob).Amount.Should().Be(100);
		Slice(side, Bob).Eligible.Should().BeTrue();
		Slice(side, Carol).Amount.Should().Be(100);
		Slice(side, Carol).Eligible.Should().BeTrue();
	}

	[Fact]
	public void FoldedBetBetweenTwoAllInLevels_StaysInTheMiddleLayer()
	{
		var stakes = new Stake[]
		{
			new(Dave, 80, HasFolded: true, IsAllIn: false),
			new(Alice, 50, HasFolded: false, IsAllIn: true),
			new(Bob, 150, HasFolded: false, IsAllIn: true),
			new(Carol, 200, HasFolded: false, IsAllIn: false)
		};

		var layers = FiveCardDrawSidePots.Build(stakes);

		layers.Should().HaveCount(3);
		AssertBalanced(layers, stakes);

		var main = layers[0];
		main.PotOrder.Should().Be(0);
		main.Amount.Should().Be(200);
		Slice(main, Dave).Amount.Should().Be(50);
		Slice(main, Dave).Eligible.Should().BeFalse();
		Slice(main, Alice).Amount.Should().Be(50);
		Slice(main, Alice).Eligible.Should().BeTrue();
		Slice(main, Bob).Amount.Should().Be(50);
		Slice(main, Bob).Eligible.Should().BeTrue();
		Slice(main, Carol).Amount.Should().Be(50);
		Slice(main, Carol).Eligible.Should().BeTrue();

		var middle = layers[1];
		middle.PotOrder.Should().Be(1);
		middle.Amount.Should().Be(230);
		Slice(middle, Dave).Amount.Should().Be(30);
		Slice(middle, Dave).Eligible.Should().BeFalse();
		middle.Slices.Should().NotContain(slice => slice.GamePlayerId == Alice);
		Slice(middle, Bob).Amount.Should().Be(100);
		Slice(middle, Bob).Eligible.Should().BeTrue();
		Slice(middle, Carol).Amount.Should().Be(100);
		Slice(middle, Carol).Eligible.Should().BeTrue();

		var top = layers[2];
		top.PotOrder.Should().Be(2);
		top.Amount.Should().Be(50);
		top.Slices.Should().ContainSingle();
		Slice(top, Carol).Amount.Should().Be(50);
		Slice(top, Carol).Eligible.Should().BeTrue();
		top.Slices.Should().NotContain(slice => slice.GamePlayerId == Dave);
		top.Slices.Should().NotContain(slice => slice.GamePlayerId == Alice);
		top.Slices.Should().NotContain(slice => slice.GamePlayerId == Bob);
	}

	[Fact]
	public void NoFoldedShortMoney_SplitsAtEachAllInLevel()
	{
		var stakes = new Stake[]
		{
			new(Alice, 100, HasFolded: false, IsAllIn: true),
			new(Bob, 300, HasFolded: false, IsAllIn: true),
			new(Carol, 300, HasFolded: false, IsAllIn: false)
		};

		var layers = FiveCardDrawSidePots.Build(stakes);

		layers.Should().HaveCount(2);
		AssertBalanced(layers, stakes);

		var main = layers[0];
		main.PotOrder.Should().Be(0);
		main.Amount.Should().Be(300);
		main.Slices.Should().OnlyContain(slice => slice.Amount == 100 && slice.Eligible);
		main.Slices.Select(slice => slice.GamePlayerId).Should().BeEquivalentTo([Alice, Bob, Carol]);

		var side = layers[1];
		side.PotOrder.Should().Be(1);
		side.Amount.Should().Be(400);
		side.Slices.Should().NotContain(slice => slice.GamePlayerId == Alice);
		Slice(side, Bob).Amount.Should().Be(200);
		Slice(side, Bob).Eligible.Should().BeTrue();
		Slice(side, Carol).Amount.Should().Be(200);
		Slice(side, Carol).Eligible.Should().BeTrue();
	}

	[Fact]
	public void NoAllIn_ReturnsNoLayers()
	{
		var stakes = new Stake[]
		{
			new(Alice, 40, HasFolded: false, IsAllIn: false),
			new(Bob, 40, HasFolded: false, IsAllIn: false)
		};

		FiveCardDrawSidePots.Build(stakes).Should().BeEmpty();
	}

	private static FiveCardDrawSidePots.Slice Slice(Layer layer, Guid playerId) =>
		layer.Slices.Single(slice => slice.GamePlayerId == playerId);

	private static void AssertBalanced(IReadOnlyList<Layer> layers, IReadOnlyList<Stake> stakes)
	{
		layers.Sum(layer => layer.Amount).Should().Be(stakes.Sum(stake => stake.Contributed));
		foreach (var layer in layers)
			layer.Slices.Sum(slice => slice.Amount).Should().Be(layer.Amount);
	}
}
