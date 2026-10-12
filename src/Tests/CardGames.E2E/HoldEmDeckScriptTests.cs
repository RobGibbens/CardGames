using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class HoldEmDeckScriptTests
{
	[Fact]
	public void Build_DealsBothHoleCardsToTheSeatLeftOfTheDealer_ThenTheBoard()
	{
		var deck = HoldEmDeckScript.Build(
			dealerSeat: 0,
			new Dictionary<int, IReadOnlyList<string>>
			{
				[0] = ["Qs", "Jc"],
				[1] = ["Ah", "Kd"]
			},
			["2c", "3d", "4h", "5s", "6c"]);

		Assert.Equal(
			["Ah", "Kd", "Qs", "Jc", "2c", "3d", "4h", "5s", "6c"],
			deck.Take(9));
		Assert.Equal(52, deck.Count);
		Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
	}

	[Fact]
	public void Build_OrdersFourSeatsLeftOfTheDealer()
	{
		var deck = HoldEmDeckScript.Build(
			dealerSeat: 0,
			new Dictionary<int, IReadOnlyList<string>>
			{
				[0] = ["As", "Kd"],
				[1] = ["7c", "6d"],
				[2] = ["Kh", "Qh"],
				[3] = ["Qs", "Qd"]
			},
			["Tc", "9d", "8h", "2c", "3s"]);

		Assert.Equal(
			[
				"7c", "6d",
				"Kh", "Qh",
				"Qs", "Qd",
				"As", "Kd",
				"Tc", "9d", "8h", "2c", "3s"
			],
			deck.Take(13));
	}

	[Fact]
	public void Build_UsesEmptySeatsInTheRotation()
	{
		var deck = HoldEmDeckScript.Build(
			dealerSeat: 0,
			new Dictionary<int, IReadOnlyList<string>>
			{
				[0] = ["As", "Kd"],
				[2] = ["Qh", "Jh"]
			},
			["2c", "3d", "4h", "5s", "6c"]);

		Assert.Equal(["Qh", "Jh", "As", "Kd"], deck.Take(4));
	}

	[Fact]
	public void Build_NormalizesCardCodes()
	{
		var deck = HoldEmDeckScript.Build(
			dealerSeat: 0,
			new Dictionary<int, IReadOnlyList<string>>
			{
				[0] = ["qs", "jc"],
				[1] = ["ah", "KD"]
			},
			["2C", "3d", "4h", "5s", "6c"]);

		Assert.Equal(["Ah", "Kd", "Qs", "Jc", "2c"], deck.Take(5));
	}

	[Fact]
	public void Build_RejectsARepeatedCard()
	{
		var error = Assert.Throws<ArgumentException>(() => HoldEmDeckScript.Build(
			dealerSeat: 0,
			new Dictionary<int, IReadOnlyList<string>>
			{
				[0] = ["Ah", "Kd"],
				[1] = ["Ah", "Qs"]
			},
			["2c", "3d", "4h", "5s", "6c"]));

		Assert.Contains("Ah", error.Message, StringComparison.Ordinal);
	}
}
