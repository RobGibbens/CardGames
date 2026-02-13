using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Guards against duplicate-card bugs by ensuring street flow handlers reuse an existing persisted deck.
/// </summary>
public class StreetFlowDeckIdempotencyTests : IntegrationTestBase
{
    [Fact]
    public async Task FollowTheQueen_DealCardsAsync_WithExistingDeck_DoesNotCreateDuplicateCards()
    {
        // Arrange
        var handler = new FollowTheQueenFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        await SeedPersistedDeckAsync(setup.Game.Id, setup.Game.CurrentHandNumber);

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await DbContext.SaveChangesAsync();

        // Assert
        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.HandNumber == setup.Game.CurrentHandNumber)
            .ToListAsync();

        cards.Should().HaveCount(52);
        cards.Select(c => (c.Suit, c.Symbol)).Distinct().Should().HaveCount(52);
        cards.Count(c => c.Location == CardLocation.Deck).Should().Be(40); // 52 - (4 players * 3 cards)
    }

    [Fact]
    public async Task Baseball_DealCardsAsync_WithExistingDeck_DoesNotCreateDuplicateCards()
    {
        // Arrange
        var handler = new BaseballFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        await SeedPersistedDeckAsync(setup.Game.Id, setup.Game.CurrentHandNumber);

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await DbContext.SaveChangesAsync();

        // Assert
        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.HandNumber == setup.Game.CurrentHandNumber)
            .ToListAsync();

        cards.Should().HaveCount(52);
        cards.Select(c => (c.Suit, c.Symbol)).Distinct().Should().HaveCount(52);
        cards.Count(c => c.Location == CardLocation.Deck).Should().Be(40); // 52 - (4 players * 3 cards)
    }

    private async Task SeedPersistedDeckAsync(Guid gameId, int handNumber)
    {
        var deckCards = new List<GameCard>();
        var dealOrder = 0;

        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var symbol in Enum.GetValues<CardSymbol>())
            {
                deckCards.Add(new GameCard
                {
                    GameId = gameId,
                    GamePlayerId = null,
                    HandNumber = handNumber,
                    Suit = suit,
                    Symbol = symbol,
                    Location = CardLocation.Deck,
                    DealOrder = dealOrder++,
                    IsVisible = false,
                    IsDiscarded = false,
                    DealtAt = DateTimeOffset.UtcNow
                });
            }
        }

        DbContext.GameCards.AddRange(deckCards);
        await DbContext.SaveChangesAsync();
    }
}
