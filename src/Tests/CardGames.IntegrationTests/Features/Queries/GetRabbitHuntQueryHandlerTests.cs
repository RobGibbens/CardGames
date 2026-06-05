using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using ContractCardSuit = CardGames.Poker.Api.Contracts.CardSuit;
using ContractCardSymbol = CardGames.Poker.Api.Contracts.CardSymbol;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;
using CoreCard = CardGames.Core.French.Cards.Card;
using CoreSuit = CardGames.Core.French.Cards.Suit;
using CoreSymbol = CardGames.Core.French.Cards.Symbol;
using GameStatus = CardGames.Poker.Api.Data.Entities.GameStatus;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetRabbitHuntQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task GetRabbitHunt_HoldEmPreFlopFoldout_ReturnsExactRemainingBoardFromDeck()
	{
		var setup = await CreateDealtCommunityGameAsync("HOLDEM", 3);
		ConfigureCurrentUser(setup.Players[0]);

		var heroCards = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.GamePlayerId == setup.GamePlayers[0].Id
			               && !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.Take(2)
			.ToListAsync();

		heroCards[0].Suit = CardSuit.Hearts;
		heroCards[0].Symbol = CardSymbol.Ace;
		heroCards[1].Suit = CardSuit.Hearts;
		heroCards[1].Symbol = CardSymbol.King;

		var configuredRunout = new[]
		{
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Queen },
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Jack },
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Ten },
			new { Suit = CardSuit.Clubs, Symbol = CardSymbol.Deuce },
			new { Suit = CardSuit.Diamonds, Symbol = CardSymbol.Three }
		};

		var deckCardsToRewrite = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(configuredRunout.Length)
			.ToListAsync();

		deckCardsToRewrite.Should().HaveCount(configuredRunout.Length);

		for (var index = 0; index < configuredRunout.Length; index++)
		{
			deckCardsToRewrite[index].Suit = configuredRunout[index].Suit;
			deckCardsToRewrite[index].Symbol = configuredRunout[index].Symbol;
		}

		await DbContext.SaveChangesAsync();

		var freshContext = GetFreshDbContext();
		var expectedBoard = await freshContext.GameCards
			.AsNoTracking()
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(5)
			.Select(card => new { Suit = MapContractSuit(card.Suit), Symbol = MapContractSymbol(card.Symbol) })
			.ToListAsync();

		(await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Fold))).IsT0.Should()
			.BeTrue();
		(await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Fold))).IsT0.Should()
			.BeTrue();

		var result = await Mediator.Send(new GetRabbitHuntQuery(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var rabbitHunt = result.AsT0;
		var expectedDescription = HandDescriptionFormatter.GetHandDescription(
			new HoldemHand(
				[new CoreCard(CoreSuit.Hearts, CoreSymbol.Ace), new CoreCard(CoreSuit.Hearts, CoreSymbol.King)],
				[
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Queen),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Jack),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Ten),
					new CoreCard(CoreSuit.Clubs, CoreSymbol.Deuce),
					new CoreCard(CoreSuit.Diamonds, CoreSymbol.Three)
				]));

		rabbitHunt.CommunityCards.Should().HaveCount(5);
		rabbitHunt.PlayerCards.Should().HaveCount(2);
		rabbitHunt.PlayerCards.Should().Equal(
			new ShowdownCard(ContractCardSuit.Hearts, ContractCardSymbol.Ace),
			new ShowdownCard(ContractCardSuit.Hearts, ContractCardSymbol.King));
		rabbitHunt.NewlyRevealedCards.Should().HaveCount(5);
		rabbitHunt.PreviouslyVisibleCards.Should().BeEmpty();
		rabbitHunt.ProjectedHandEvaluationDescription.Should().Be(expectedDescription);
		rabbitHunt.CommunityCards
			.Select(card => new { card.Card.Suit, card.Card.Symbol })
			.Should().Equal(expectedBoard.Select(card => new
				{ Suit = (ContractCardSuit?)card.Suit, Symbol = (ContractCardSymbol?)card.Symbol }));
		rabbitHunt.CommunityCards.Select(card => card.DealOrder).Should().Equal(1, 2, 3, 4, 5);
	}

	[Fact]
	public async Task GetRabbitHunt_KlondikePreFlopFoldout_IncludesHiddenKlondikeCardAndRemainingDeck()
	{
		var setup = await CreateDealtCommunityGameAsync("KLONDIKE", 3);
		ConfigureCurrentUser(setup.Players[0]);

		var heroCards = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.GamePlayerId == setup.GamePlayers[0].Id
			               && !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.Take(2)
			.ToListAsync();

		heroCards[0].Suit = CardSuit.Hearts;
		heroCards[0].Symbol = CardSymbol.King;
		heroCards[1].Suit = CardSuit.Spades;
		heroCards[1].Symbol = CardSymbol.King;

		var hiddenKlondikeCard = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Community
			               && card.DealtAtPhase == "KlondikeCard")
			.SingleAsync();

		hiddenKlondikeCard.Suit = CardSuit.Clubs;
		hiddenKlondikeCard.Symbol = CardSymbol.Deuce;

		var configuredRunout = new[]
		{
			new { Suit = CardSuit.Diamonds, Symbol = CardSymbol.King },
			new { Suit = CardSuit.Spades, Symbol = CardSymbol.Five },
			new { Suit = CardSuit.Diamonds, Symbol = CardSymbol.Eight },
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Three },
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Four }
		};

		var deckCardsToRewrite = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(configuredRunout.Length)
			.ToListAsync();

		for (var index = 0; index < configuredRunout.Length; index++)
		{
			deckCardsToRewrite[index].Suit = configuredRunout[index].Suit;
			deckCardsToRewrite[index].Symbol = configuredRunout[index].Symbol;
		}

		await DbContext.SaveChangesAsync();

		var freshContext = GetFreshDbContext();

		var expectedRunout = await freshContext.GameCards
			.AsNoTracking()
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(5)
			.Select(card => new { Suit = MapContractSuit(card.Suit), Symbol = MapContractSymbol(card.Symbol) })
			.ToListAsync();

		(await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Fold))).IsT0.Should()
			.BeTrue();
		(await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Fold))).IsT0.Should()
			.BeTrue();

		var result = await Mediator.Send(new GetRabbitHuntQuery(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var rabbitHunt = result.AsT0;
		var expectedDescription = HandDescriptionFormatter.GetHandDescription(
			new KlondikeHand(
				[new CoreCard(CoreSuit.Hearts, CoreSymbol.King), new CoreCard(CoreSuit.Spades, CoreSymbol.King)],
				[
					new CoreCard(CoreSuit.Clubs, CoreSymbol.Deuce),
					new CoreCard(CoreSuit.Diamonds, CoreSymbol.King),
					new CoreCard(CoreSuit.Spades, CoreSymbol.Five),
					new CoreCard(CoreSuit.Diamonds, CoreSymbol.Eight),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Three),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Four)
				],
				new CoreCard(CoreSuit.Clubs, CoreSymbol.Deuce)));

		rabbitHunt.CommunityCards.Should().HaveCount(6);
		rabbitHunt.NewlyRevealedCards.Should().HaveCount(6);
		rabbitHunt.PlayerCards.Should().HaveCount(2);
		rabbitHunt.PlayerCards.Should().Equal(
			new ShowdownCard(ContractCardSuit.Hearts, ContractCardSymbol.King),
			new ShowdownCard(ContractCardSuit.Spades, ContractCardSymbol.King));
		rabbitHunt.ProjectedHandEvaluationDescription.Should().Be(expectedDescription);

		rabbitHunt.CommunityCards[0].DealOrder.Should().Be(0);
		rabbitHunt.CommunityCards[0].IsKlondikeCard.Should().BeTrue();
		rabbitHunt.CommunityCards[0].Card.Suit.Should().Be(MapContractSuit(hiddenKlondikeCard.Suit));
		rabbitHunt.CommunityCards[0].Card.Symbol.Should().Be(MapContractSymbol(hiddenKlondikeCard.Symbol));
		rabbitHunt.CommunityCards
			.Skip(1)
			.Select(card => new { card.Card.Suit, card.Card.Symbol })
			.Should().Equal(expectedRunout.Select(card => new
				{ Suit = (ContractCardSuit?)card.Suit, Symbol = (ContractCardSymbol?)card.Symbol }));
	}

	[Fact]
	public async Task GetRabbitHunt_IrishHoldemFoldedRequesterBeforeDiscard_ReturnsProjectedDescription()
	{
		var setup = await CreateDealtCommunityGameAsync("IRISHHOLDEM", 3);
		ConfigureCurrentUser(setup.Players[0]);

		var heroCards = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.GamePlayerId == setup.GamePlayers[0].Id
			               && !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.Take(4)
			.ToListAsync();

		heroCards[0].Suit = CardSuit.Hearts;
		heroCards[0].Symbol = CardSymbol.Ace;
		heroCards[1].Suit = CardSuit.Hearts;
		heroCards[1].Symbol = CardSymbol.King;
		heroCards[2].Suit = CardSuit.Spades;
		heroCards[2].Symbol = CardSymbol.Queen;
		heroCards[3].Suit = CardSuit.Clubs;
		heroCards[3].Symbol = CardSymbol.Queen;

		var configuredRunout = new[]
		{
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Jack },
			new { Suit = CardSuit.Hearts, Symbol = CardSymbol.Ten },
			new { Suit = CardSuit.Clubs, Symbol = CardSymbol.Deuce },
			new { Suit = CardSuit.Diamonds, Symbol = CardSymbol.Three },
			new { Suit = CardSuit.Spades, Symbol = CardSymbol.Four }
		};

		var deckCardsToRewrite = await DbContext.GameCards
			.Where(card => card.GameId == setup.Game.Id
			               && card.HandNumber == setup.Game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(configuredRunout.Length)
			.ToListAsync();

		deckCardsToRewrite.Should().HaveCount(configuredRunout.Length);

		for (var index = 0; index < configuredRunout.Length; index++)
		{
			deckCardsToRewrite[index].Suit = configuredRunout[index].Suit;
			deckCardsToRewrite[index].Symbol = configuredRunout[index].Symbol;
		}

		setup.GamePlayers[0].HasFolded = true;
		setup.Game.CurrentPhase = "Complete";

		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new GetRabbitHuntQuery(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var rabbitHunt = result.AsT0;
		var expectedDescription = HandDescriptionFormatter.GetHandDescription(
			new HoldemHand(
				[
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Ace),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.King),
					new CoreCard(CoreSuit.Spades, CoreSymbol.Queen),
					new CoreCard(CoreSuit.Clubs, CoreSymbol.Queen)
				],
				[
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Jack),
					new CoreCard(CoreSuit.Hearts, CoreSymbol.Ten),
					new CoreCard(CoreSuit.Clubs, CoreSymbol.Deuce),
					new CoreCard(CoreSuit.Diamonds, CoreSymbol.Three),
					new CoreCard(CoreSuit.Spades, CoreSymbol.Four)
				]));

		rabbitHunt.NewlyRevealedCards.Should().HaveCount(5);
		rabbitHunt.PlayerCards.Should().HaveCount(4);
		rabbitHunt.PlayerCards.Should().Equal(
			new ShowdownCard(ContractCardSuit.Hearts, ContractCardSymbol.Ace),
			new ShowdownCard(ContractCardSuit.Hearts, ContractCardSymbol.King),
			new ShowdownCard(ContractCardSuit.Spades, ContractCardSymbol.Queen),
			new ShowdownCard(ContractCardSuit.Clubs, ContractCardSymbol.Queen));
		rabbitHunt.ProjectedHandEvaluationDescription.Should().Be(expectedDescription);
	}

	[Fact]
	public async Task GetRabbitHunt_BeforeHandIsOver_ReturnsNotAvailable()
	{
		var setup = await CreateDealtCommunityGameAsync("HOLDEM", 3);
		ConfigureCurrentUser(setup.Players[0]);

		var result = await Mediator.Send(new GetRabbitHuntQuery(setup.Game.Id));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(GetRabbitHuntErrorCode.RabbitHuntNotAvailable);
	}

	private void ConfigureCurrentUser(Player player)
	{
		var currentUser = Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		currentUser.Should().BeOfType<FakeCurrentUserService>();

		var fakeCurrentUser = (FakeCurrentUserService)currentUser;
		fakeCurrentUser.UserEmail = player.Email;
		fakeCurrentUser.UserName = player.Name;
		fakeCurrentUser.UserId = player.ExternalId;
		fakeCurrentUser.IsAuthenticated = true;
	}

	private async Task<GameSetup> CreateDealtCommunityGameAsync(string gameTypeCode, int playerCount)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, gameTypeCode, playerCount);
		var game = setup.Game;

		game.SmallBlind = 5;
		game.BigBlind = 10;
		game.DealerPosition = 0;
		game.CurrentHandNumber = 1;
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

		var handler = FlowHandlerFactory.GetHandler(gameTypeCode);
		await handler.DealCardsAsync(DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

		return setup;
	}

	private static ContractCardSuit MapContractSuit(CardSuit suit)
	{
		return suit switch
		{
			CardSuit.Hearts => ContractCardSuit.Hearts,
			CardSuit.Diamonds => ContractCardSuit.Diamonds,
			CardSuit.Spades => ContractCardSuit.Spades,
			CardSuit.Clubs => ContractCardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, null)
		};
	}

	private static ContractCardSymbol MapContractSymbol(CardSymbol symbol)
	{
		return symbol switch
		{
			CardSymbol.Deuce => ContractCardSymbol.Deuce,
			CardSymbol.Three => ContractCardSymbol.Three,
			CardSymbol.Four => ContractCardSymbol.Four,
			CardSymbol.Five => ContractCardSymbol.Five,
			CardSymbol.Six => ContractCardSymbol.Six,
			CardSymbol.Seven => ContractCardSymbol.Seven,
			CardSymbol.Eight => ContractCardSymbol.Eight,
			CardSymbol.Nine => ContractCardSymbol.Nine,
			CardSymbol.Ten => ContractCardSymbol.Ten,
			CardSymbol.Jack => ContractCardSymbol.Jack,
			CardSymbol.Queen => ContractCardSymbol.Queen,
			CardSymbol.King => ContractCardSymbol.King,
			CardSymbol.Ace => ContractCardSymbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null)
		};
	}
}