using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Testing;

namespace CardGames.IntegrationTests.Games.HoldEm;

public class ScriptedDeckStartHandTests : IntegrationTestBase
{
	[Fact]
	public async Task StartHand_UsesTheScriptOnce_ThenShufflesTheNextHand()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDEM", 2);
		var script = BuildDeck("Ah", "Kd", "Qs", "Jc", "2c", "3d", "4h", "5s", "6c");
		Assert.True(ScriptedDeckStore.TryParse(script, out var cards, out var message), message);
		ScriptedDeckStore.Instance.Set(setup.Game.Id, cards);

		var first = await Mediator.Send(new StartHandCommand(setup.Game.Id));
		first.IsT0.Should().BeTrue();

		var firstContext = GetFreshDbContext();
		var firstGame = await firstContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		var firstCards = await LoadCardsAsync(firstContext, setup.Game.Id, firstGame.CurrentHandNumber);

		var seat1 = setup.GamePlayers.Single(player => player.SeatPosition == 1);
		var seat0 = setup.GamePlayers.Single(player => player.SeatPosition == 0);
		Codes(firstCards, seat1.Id).Should().Equal("Ah", "Kd");
		Codes(firstCards, seat0.Id).Should().Equal("Qs", "Jc");
		Board(firstCards).Should().Equal("2c", "3d", "4h", "5s", "6c");
		Sequence(firstCards).Should().Equal(script);

		var tracked = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		tracked.CurrentPhase = nameof(Phases.Complete);
		await DbContext.SaveChangesAsync();

		var second = await Mediator.Send(new StartHandCommand(setup.Game.Id));
		second.IsT0.Should().BeTrue();

		var secondContext = GetFreshDbContext();
		var secondGame = await secondContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		var secondCards = await LoadCardsAsync(secondContext, setup.Game.Id, secondGame.CurrentHandNumber);
		Sequence(secondCards).Should().NotEqual(script);
	}

	private static async Task<List<GameCard>> LoadCardsAsync(CardsDbContext context, Guid gameId, int handNumber)
	{
		return await context.GameCards
			.AsNoTracking()
			.Where(card => card.GameId == gameId && card.HandNumber == handNumber && !card.IsDiscarded)
			.ToListAsync();
	}

	private static IEnumerable<string> Codes(IEnumerable<GameCard> cards, Guid gamePlayerId)
	{
		return cards
			.Where(card => card.Location == CardLocation.Hand && card.GamePlayerId == gamePlayerId)
			.OrderBy(card => card.DealOrder)
			.Select(Format);
	}

	private static IEnumerable<string> Board(IEnumerable<GameCard> cards)
	{
		return cards
			.Where(card => card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(5)
			.Select(Format);
	}

	private static IEnumerable<string> Sequence(IEnumerable<GameCard> cards)
	{
		var dealt = cards
			.Where(card => card.Location == CardLocation.Hand)
			.OrderBy(card => card.DealOrder)
			.Select(Format);
		var remaining = cards
			.Where(card => card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Select(Format);
		return dealt.Concat(remaining);
	}

	private static string Format(GameCard card)
	{
		var rank = card.Symbol switch
		{
			CardSymbol.Ten => "T",
			CardSymbol.Jack => "J",
			CardSymbol.Queen => "Q",
			CardSymbol.King => "K",
			CardSymbol.Ace => "A",
			_ => ((int)card.Symbol).ToString()
		};
		var suit = card.Suit switch
		{
			CardSuit.Hearts => "h",
			CardSuit.Diamonds => "d",
			CardSuit.Spades => "s",
			CardSuit.Clubs => "c",
			_ => throw new ArgumentOutOfRangeException(nameof(card))
		};
		return rank + suit;
	}

	private static IReadOnlyList<string> BuildDeck(params string[] prefix)
	{
		string[] standard =
		[
			"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
			"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
			"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
			"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
		];
		var used = new HashSet<string>(prefix, StringComparer.Ordinal);
		return prefix.Concat(standard.Where(card => used.Add(card))).ToArray();
	}
}
