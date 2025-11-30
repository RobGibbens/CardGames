using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Dealing;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Events;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Features.Dealing;

/// <summary>
/// Service for emitting deal card events via SignalR.
/// Provides methods to broadcast card dealing operations to clients for animation.
/// </summary>
public interface IDealingEventEmitter
{
    /// <summary>
    /// Emits a DealingStarted event to signal the beginning of a dealing phase.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="phaseName">The name of the phase (e.g., "Hole Cards", "Flop").</param>
    /// <param name="totalCards">The total number of cards to be dealt.</param>
    /// <param name="replaySeed">Optional seed for deterministic replay.</param>
    Task EmitDealingStartedAsync(Guid gameId, string phaseName, int totalCards, int? replaySeed = null);

    /// <summary>
    /// Emits a DealCard event for a single card deal.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="dealResult">The result of the deal operation.</param>
    /// <param name="hideCard">Whether to hide the card details (for face-down cards to non-owners).</param>
    Task EmitDealCardAsync(Guid gameId, DealResult dealResult, bool hideCard = false);

    /// <summary>
    /// Emits DealCard events for multiple cards in sequence.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="dealResults">The results of the deal operations.</param>
    /// <param name="hideCardsForRecipient">Function to determine if a card should be hidden based on recipient.</param>
    Task EmitDealCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, Func<string, bool>? hideCardsForRecipient = null);

    /// <summary>
    /// Emits a DealingCompleted event to signal the end of a dealing phase.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="phaseName">The name of the phase that completed.</param>
    Task EmitDealingCompletedAsync(Guid gameId, string phaseName);

    /// <summary>
    /// Emits private hole card events to each player for their own cards,
    /// while broadcasting hidden card events to all other players.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="dealResults">The hole cards dealt to players.</param>
    /// <param name="connectionMapping">Service to get connection IDs for players.</param>
    Task EmitPrivateHoleCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, IConnectionMappingService connectionMapping);

    /// <summary>
    /// Emits community card events to all players (cards visible to everyone).
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="streetName">The name of the street (e.g., "Flop", "Turn", "River").</param>
    /// <param name="dealResults">The community cards dealt.</param>
    Task EmitCommunityCardsAsync(Guid gameId, string streetName, IReadOnlyList<DealResult> dealResults);

    /// <summary>
    /// Emits stud card events with mixed visibility (face-up visible to all, face-down private).
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="dealResults">The cards dealt.</param>
    /// <param name="connectionMapping">Service to get connection IDs for players.</param>
    Task EmitStudCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, IConnectionMappingService connectionMapping);
}

/// <summary>
/// Default implementation of IDealingEventEmitter using SignalR.
/// </summary>
public class DealingEventEmitter : IDealingEventEmitter
{
    private readonly IHubContext<GameHub> _hubContext;

    public DealingEventEmitter(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc/>
    public async Task EmitDealingStartedAsync(Guid gameId, string phaseName, int totalCards, int? replaySeed = null)
    {
        var dealingStartedEvent = new DealingStartedEvent(
            gameId,
            DateTime.UtcNow,
            phaseName,
            totalCards,
            replaySeed);

        await _hubContext.Clients.Group(gameId.ToString())
            .SendAsync("DealingStarted", dealingStartedEvent);
    }

    /// <inheritdoc/>
    public async Task EmitDealCardAsync(Guid gameId, DealResult dealResult, bool hideCard = false)
    {
        var cardDto = hideCard ? null : dealResult.Card.ToDto();
        var isFaceDown = IsCardFaceDown(dealResult.CardType);

        var dealCardEvent = new DealCardEvent(
            gameId,
            DateTime.UtcNow,
            dealResult.Recipient,
            cardDto,
            MapCardType(dealResult.CardType),
            dealResult.DealSequence,
            isFaceDown);

        await _hubContext.Clients.Group(gameId.ToString())
            .SendAsync("DealCard", dealCardEvent);
    }

    /// <inheritdoc/>
    public async Task EmitDealCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, Func<string, bool>? hideCardsForRecipient = null)
    {
        foreach (var result in dealResults)
        {
            var hideCard = hideCardsForRecipient?.Invoke(result.Recipient) ?? false;
            await EmitDealCardAsync(gameId, result, hideCard);
        }
    }

    /// <inheritdoc/>
    public async Task EmitDealingCompletedAsync(Guid gameId, string phaseName)
    {
        var dealingCompletedEvent = new DealingCompletedEvent(
            gameId,
            DateTime.UtcNow,
            phaseName);

        await _hubContext.Clients.Group(gameId.ToString())
            .SendAsync("DealingCompleted", dealingCompletedEvent);
    }

    /// <inheritdoc/>
    public async Task EmitPrivateHoleCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, IConnectionMappingService connectionMapping)
    {
        var tableId = gameId.ToString();

        foreach (var result in dealResults)
        {
            if (result.CardType != Poker.Dealing.DealCardType.HoleCard)
            {
                continue;
            }

            var recipientConnectionId = connectionMapping.GetConnectionId(result.Recipient, tableId);

            // Send the full card details to the recipient
            if (recipientConnectionId != null)
            {
                var privateEvent = new DealCardEvent(
                    gameId,
                    DateTime.UtcNow,
                    result.Recipient,
                    result.Card.ToDto(),
                    Shared.Events.DealCardType.HoleCard,
                    result.DealSequence,
                    IsFaceDown: true);

                await _hubContext.Clients.Client(recipientConnectionId)
                    .SendAsync("DealCard", privateEvent);
            }

            // Send hidden card event to everyone else
            var hiddenEvent = new DealCardEvent(
                gameId,
                DateTime.UtcNow,
                result.Recipient,
                Card: null,
                Shared.Events.DealCardType.HoleCard,
                result.DealSequence,
                IsFaceDown: true);

            if (recipientConnectionId != null)
            {
                await _hubContext.Clients.GroupExcept(tableId, recipientConnectionId)
                    .SendAsync("DealCard", hiddenEvent);
            }
            else
            {
                await _hubContext.Clients.Group(tableId)
                    .SendAsync("DealCard", hiddenEvent);
            }
        }
    }

    /// <inheritdoc/>
    public async Task EmitCommunityCardsAsync(Guid gameId, string streetName, IReadOnlyList<DealResult> dealResults)
    {
        var tableId = gameId.ToString();

        foreach (var result in dealResults)
        {
            var isFaceDown = result.CardType == Poker.Dealing.DealCardType.BurnCard;
            var cardDto = isFaceDown ? null : result.Card.ToDto();

            var communityEvent = new DealCardEvent(
                gameId,
                DateTime.UtcNow,
                result.Recipient,
                cardDto,
                MapCardType(result.CardType),
                result.DealSequence,
                isFaceDown);

            await _hubContext.Clients.Group(tableId)
                .SendAsync("DealCard", communityEvent);
        }
    }

    /// <inheritdoc/>
    public async Task EmitStudCardsAsync(Guid gameId, IReadOnlyList<DealResult> dealResults, IConnectionMappingService connectionMapping)
    {
        var tableId = gameId.ToString();

        foreach (var result in dealResults)
        {
            var isFaceUp = result.CardType == Poker.Dealing.DealCardType.FaceUpCard;

            if (isFaceUp)
            {
                // Face-up cards are visible to everyone
                var publicEvent = new DealCardEvent(
                    gameId,
                    DateTime.UtcNow,
                    result.Recipient,
                    result.Card.ToDto(),
                    Shared.Events.DealCardType.FaceUpCard,
                    result.DealSequence,
                    IsFaceDown: false);

                await _hubContext.Clients.Group(tableId)
                    .SendAsync("DealCard", publicEvent);
            }
            else
            {
                // Face-down cards: send full details to owner, hidden to others
                var recipientConnectionId = connectionMapping.GetConnectionId(result.Recipient, tableId);

                if (recipientConnectionId != null)
                {
                    var privateEvent = new DealCardEvent(
                        gameId,
                        DateTime.UtcNow,
                        result.Recipient,
                        result.Card.ToDto(),
                        MapCardType(result.CardType),
                        result.DealSequence,
                        IsFaceDown: true);

                    await _hubContext.Clients.Client(recipientConnectionId)
                        .SendAsync("DealCard", privateEvent);
                }

                var hiddenEvent = new DealCardEvent(
                    gameId,
                    DateTime.UtcNow,
                    result.Recipient,
                    Card: null,
                    MapCardType(result.CardType),
                    result.DealSequence,
                    IsFaceDown: true);

                if (recipientConnectionId != null)
                {
                    await _hubContext.Clients.GroupExcept(tableId, recipientConnectionId)
                        .SendAsync("DealCard", hiddenEvent);
                }
                else
                {
                    await _hubContext.Clients.Group(tableId)
                        .SendAsync("DealCard", hiddenEvent);
                }
            }
        }
    }

    /// <summary>
    /// Determines if a card should be dealt face down based on its type.
    /// </summary>
    /// <param name="cardType">The type of card being dealt.</param>
    /// <returns>True if the card should be face down, false otherwise.</returns>
    private static bool IsCardFaceDown(Poker.Dealing.DealCardType cardType)
    {
        return cardType switch
        {
            Poker.Dealing.DealCardType.HoleCard => true,
            Poker.Dealing.DealCardType.BurnCard => true,
            Poker.Dealing.DealCardType.FaceUpCard => false,
            Poker.Dealing.DealCardType.CommunityCard => false,
            _ => true
        };
    }

    private static Shared.Events.DealCardType MapCardType(Poker.Dealing.DealCardType cardType)
    {
        return cardType switch
        {
            Poker.Dealing.DealCardType.HoleCard => Shared.Events.DealCardType.HoleCard,
            Poker.Dealing.DealCardType.FaceUpCard => Shared.Events.DealCardType.FaceUpCard,
            Poker.Dealing.DealCardType.CommunityCard => Shared.Events.DealCardType.CommunityCard,
            Poker.Dealing.DealCardType.BurnCard => Shared.Events.DealCardType.BurnCard,
            _ => Shared.Events.DealCardType.HoleCard
        };
    }
}
