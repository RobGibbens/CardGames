using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public sealed class SelectShowcaseCommandHandler(CardsDbContext context)
    : IRequestHandler<SelectShowcaseCommand, OneOf<SelectShowcaseSuccessful, SelectShowcaseError>>
{
    private const int InitialHoleCardCount = 5;

    public async Task<OneOf<SelectShowcaseSuccessful, SelectShowcaseError>> Handle(
        SelectShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var game = await context.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GameType)
            .Include(g => g.GameCards.Where(gc => gc.HandNumber == context.Games
                .Where(g2 => g2.Id == command.GameId)
                .Select(g2 => g2.CurrentHandNumber)
                .FirstOrDefault() && !gc.IsDiscarded))
            .Include(g => g.Pots)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new SelectShowcaseError
            {
                Message = $"Game with ID '{command.GameId}' was not found.",
                Code = SelectShowcaseErrorCode.GameNotFound
            };
        }

        var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
        if (!string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase))
        {
            return new SelectShowcaseError
            {
                Message = $"Showcase selection is not supported for game type '{gameTypeCode}'.",
                Code = SelectShowcaseErrorCode.NotInShowcasePhase
            };
        }

        if (!string.Equals(game.CurrentPhase, nameof(Phases.DrawPhase), StringComparison.OrdinalIgnoreCase))
        {
            return new SelectShowcaseError
            {
                Message = $"Cannot select a showcase card during '{game.CurrentPhase}'. Showcase selection is only allowed in '{nameof(Phases.DrawPhase)}'.",
                Code = SelectShowcaseErrorCode.NotInShowcasePhase
            };
        }

        var activePlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active)
            .OrderBy(gp => gp.SeatPosition)
            .ToList();

        var eligiblePlayers = activePlayers
            .Where(gp => !gp.HasFolded && !gp.HasDrawnThisRound)
            .ToList();

        if (eligiblePlayers.Count == 0)
        {
            return new SelectShowcaseError
            {
                Message = "No eligible players remain to choose a showcase card.",
                Code = SelectShowcaseErrorCode.NoEligiblePlayers
            };
        }

        var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
        var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == requestedSeatIndex);
        if (currentPlayer is null || currentPlayer.HasFolded)
        {
            return new SelectShowcaseError
            {
                Message = "The acting player could not be found for showcase selection.",
                Code = SelectShowcaseErrorCode.NotPlayerTurn
            };
        }

        if (currentPlayer.HasDrawnThisRound || BobBarkerVariantState.GetSelectedShowcaseDealOrder(currentPlayer).HasValue)
        {
            return new SelectShowcaseError
            {
                Message = "This player has already chosen a showcase card.",
                Code = SelectShowcaseErrorCode.AlreadySelected
            };
        }

        var playerCards = game.GameCards
            .Where(gc => gc.GamePlayerId == currentPlayer.Id && !gc.IsDiscarded && gc.Location == CardLocation.Hand)
            .OrderBy(gc => gc.DealOrder)
            .ToList();

        if (playerCards.Count < InitialHoleCardCount)
        {
            return new SelectShowcaseError
            {
                Message = $"Player does not have enough cards. Expected {InitialHoleCardCount}, found {playerCards.Count}.",
                Code = SelectShowcaseErrorCode.InsufficientCards
            };
        }

        if (command.ShowcaseCardIndex < 0 || command.ShowcaseCardIndex >= playerCards.Count)
        {
            return new SelectShowcaseError
            {
                Message = $"Invalid showcase card index. Indices must be between 0 and {playerCards.Count - 1}.",
                Code = SelectShowcaseErrorCode.InvalidCardIndex
            };
        }

        var selectedCard = playerCards[command.ShowcaseCardIndex];
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(currentPlayer, selectedCard.DealOrder);
        currentPlayer.HasDrawnThisRound = true;

        var nextPlayerSeatIndex = FindNextPendingSelectionPlayer(activePlayers);
        var selectionComplete = nextPlayerSeatIndex < 0;
        string? nextPlayerName = null;

        if (selectionComplete)
        {
            game.CurrentDrawPlayerIndex = -1;
            StartPreFlopBetting(game, activePlayers, now);
        }
        else
        {
            game.CurrentDrawPlayerIndex = nextPlayerSeatIndex;
            game.CurrentPlayerIndex = nextPlayerSeatIndex;
            nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextPlayerSeatIndex)?.Player.Name;
        }

        game.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return new SelectShowcaseSuccessful
        {
            GameId = game.Id,
            PlayerName = currentPlayer.Player.Name,
            PlayerSeatIndex = currentPlayer.SeatPosition,
            ShowcaseCardIndex = command.ShowcaseCardIndex,
            SelectionPhaseComplete = selectionComplete,
            CurrentPhase = game.CurrentPhase,
            NextPlayerSeatIndex = nextPlayerSeatIndex,
            NextPlayerName = nextPlayerName
        };
    }

    private void StartPreFlopBetting(Game game, List<GamePlayer> activePlayers, DateTimeOffset now)
    {
        var firstActorIndex = FindFirstActivePlayerAfterBigBlind(game, activePlayers);
        var openingCurrentBet = activePlayers.Count > 0 ? activePlayers.Max(p => p.CurrentBet) : 0;

        var bettingRound = new BettingRound
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            RoundNumber = 1,
            Street = nameof(Phases.PreFlop),
            CurrentBet = openingCurrentBet,
            MinBet = game.MinBet ?? 0,
            RaiseCount = 0,
            MaxRaises = 0,
            LastRaiseAmount = 0,
            PlayersInHand = activePlayers.Count,
            PlayersActed = 0,
            CurrentActorIndex = firstActorIndex,
            LastAggressorIndex = -1,
            IsComplete = false,
            StartedAt = now
        };

        context.BettingRounds.Add(bettingRound);
        game.CurrentPhase = nameof(Phases.PreFlop);
        game.CurrentPlayerIndex = firstActorIndex;
    }

    private static int FindNextPendingSelectionPlayer(List<GamePlayer> activePlayers)
    {
        var pendingPlayers = activePlayers
            .Where(p => !p.HasFolded && !p.HasDrawnThisRound)
            .OrderBy(p => p.SeatPosition)
            .ToList();

        return pendingPlayers.Count == 0 ? -1 : pendingPlayers[0].SeatPosition;
    }

    private static int FindFirstActivePlayerAfterBigBlind(Game game, List<GamePlayer> eligiblePlayers)
    {
        if (eligiblePlayers.Count == 0)
        {
            return -1;
        }

        var sortedPlayers = eligiblePlayers.OrderBy(p => p.SeatPosition).ToList();
        var dealerIndex = sortedPlayers.FindIndex(p => p.SeatPosition == game.DealerPosition);
        if (dealerIndex == -1)
        {
            dealerIndex = sortedPlayers.Count - 1;
        }

        var bigBlindIndex = sortedPlayers.Count == 2
            ? (dealerIndex + 1) % sortedPlayers.Count
            : (dealerIndex + 2) % sortedPlayers.Count;

        var bigBlindSeat = sortedPlayers[bigBlindIndex].SeatPosition;
        return FindFirstActivePlayerAfterSeat(game, eligiblePlayers, bigBlindSeat);
    }

    private static int FindFirstActivePlayerAfterSeat(Game game, List<GamePlayer> activePlayers, int seatPosition)
    {
        if (activePlayers.Count == 0)
        {
            return -1;
        }

        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1;
        var searchIndex = (seatPosition + 1) % totalSeats;

        for (var i = 0; i < totalSeats; i++)
        {
            var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
            if (player is not null && !player.HasFolded && !player.IsAllIn)
            {
                return searchIndex;
            }

            searchIndex = (searchIndex + 1) % totalSeats;
        }

        return -1;
    }
}