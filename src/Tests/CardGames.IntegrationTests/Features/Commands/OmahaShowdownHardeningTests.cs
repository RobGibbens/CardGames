using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services.InMemoryEngine;
using CardGames.Poker.Evaluation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CardGames.IntegrationTests.Features.Commands;

public class OmahaShowdownHardeningTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_OmahaShowdown_UsesExactlyTwoHoleCardsForWinnerResolution()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext,
            PokerGameMetadataRegistry.OmahaCode,
            numberOfPlayers: 2,
            startingChips: 1000,
            ante: 0);

        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        await DatabaseSeeder.CreatePotAsync(DbContext, game, amount: 100);

        var playerOne = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
        var playerTwo = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
        var dealtAt = DateTimeOffset.UtcNow;

        // Player 1 cannot use board straight in Omaha (no two connecting hole cards).
        DbContext.GameCards.AddRange(
            CreateHoleCard(game.Id, playerOne.Id, CardSuit.Spades, CardSymbol.Ace, 1, dealtAt),
            CreateHoleCard(game.Id, playerOne.Id, CardSuit.Diamonds, CardSymbol.Ace, 2, dealtAt),
            CreateHoleCard(game.Id, playerOne.Id, CardSuit.Clubs, CardSymbol.King, 3, dealtAt),
            CreateHoleCard(game.Id, playerOne.Id, CardSuit.Hearts, CardSymbol.Queen, 4, dealtAt),

            // Player 2 can make a straight only when evaluator considers all 4 hole cards.
            // If generic showdown truncates to first 2 cards, Player 2 loses this hand.
            CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Hearts, CardSymbol.Ace, 1, dealtAt),
            CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Spades, CardSymbol.King, 2, dealtAt),
            CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Clubs, CardSymbol.Four, 3, dealtAt),
            CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Diamonds, CardSymbol.Five, 4, dealtAt),

            // Shared board: 6-7-8-9-T
            CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Six, 1, dealtAt),
            CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Seven, 2, dealtAt),
            CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Eight, 3, dealtAt),
            CreateCommunityCard(game.Id, CardSuit.Spades, CardSymbol.Nine, 4, dealtAt),
            CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Ten, 5, dealtAt));

        await DbContext.SaveChangesAsync();

        var handler = new PerformShowdownCommandHandler(
            DbContext,
            FlowHandlerFactory,
            new HandEvaluatorFactory(),
            new FakeHandHistoryRecorder(),
            new FakeHandSettlementService(),
            Options.Create(new InMemoryEngineOptions()),
            null!,
            NullLogger<PerformShowdownCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.Payouts.Should().ContainKey("Player 2");
        result.AsT0.Payouts["Player 2"].Should().Be(100);
        result.AsT0.Payouts.Should().NotContainKey("Player 1");
    }

    private static GameCard CreateHoleCard(
        Guid gameId,
        Guid gamePlayerId,
        CardSuit suit,
        CardSymbol symbol,
        int dealOrder,
        DateTimeOffset dealtAt) =>
        new()
        {
            GameId = gameId,
            GamePlayerId = gamePlayerId,
            HandNumber = 1,
            Suit = suit,
            Symbol = symbol,
            Location = CardLocation.Hole,
            DealOrder = dealOrder,
            DealtAtPhase = nameof(Phases.PreFlop),
            IsVisible = false,
            DealtAt = dealtAt
        };

    private static GameCard CreateCommunityCard(
        Guid gameId,
        CardSuit suit,
        CardSymbol symbol,
        int dealOrder,
        DateTimeOffset dealtAt) =>
        new()
        {
            GameId = gameId,
            GamePlayerId = null,
            HandNumber = 1,
            Suit = suit,
            Symbol = symbol,
            Location = CardLocation.Community,
            DealOrder = dealOrder,
            DealtAtPhase = nameof(Phases.Flop),
            IsVisible = true,
            DealtAt = dealtAt
        };
}
