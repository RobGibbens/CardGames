using System.Collections.Generic;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common.Showdown;

public class OddChipOrderTests
{
	[Fact]
	public void Order_WhenDealerIsSeatZero_PutsTheLowerSeatAfterTheDealerFirst()
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer([3, 1], dealerSeat: 0, seat => seat);

		ordered.Should().Equal(1, 3);
		OddChipOrder.Distribute(101, ordered).Should().Equal(51, 50);
	}

	[Fact]
	public void Order_WhenDealerIsSeatTwo_PutsTheSeatAboveTheDealerFirst()
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer([1, 3], dealerSeat: 2, seat => seat);

		ordered.Should().Equal(3, 1);
		OddChipOrder.Distribute(101, ordered).Should().Equal(51, 50);
	}

	[Fact]
	public void Distribute_WhenTwoChipsRemain_GivesOneChipToEachOfTheFirstTwoSeats()
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer([1, 2, 3], dealerSeat: 0, seat => seat);

		ordered.Should().Equal(1, 2, 3);
		OddChipOrder.Distribute(5, ordered).Should().Equal(2, 2, 1);
	}

	[Fact]
	public void Distribute_WhenOneWinner_GivesThatWinnerTheWholeAmount()
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer([3], dealerSeat: 0, seat => seat);

		OddChipOrder.Distribute(101, ordered).Should().Equal(101);
	}

	[Fact]
	public void Distribute_WhenThereAreNoWinners_ReturnsAnEmptyResult()
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer(
			new List<int>(),
			dealerSeat: 0,
			seat => seat);

		ordered.Should().BeEmpty();
		OddChipOrder.Distribute(101, ordered).Should().BeEmpty();
	}
}
