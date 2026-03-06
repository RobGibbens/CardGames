using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;

/// <summary>
/// Handles the <see cref="StartHandCommand"/> to start a new hand in a Texas Hold 'Em game.
/// </summary>
public class StartHandCommandHandler(
	CardsDbContext context,
	IGameFlowHandlerFactory flowHandlerFactory,
	ILogger<StartHandCommandHandler> logger)
	: IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
	public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
		StartHandCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
			.Include(g => g.GameType)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new StartHandError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = StartHandErrorCode.GameNotFound
			};
		}

		var validPhases = new[]
		{
			nameof(Phases.WaitingToStart),
			nameof(Phases.Complete)
		};

		if (!validPhases.Contains(game.CurrentPhase))
		{
			return new StartHandError
			{
				Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase. " +
				          $"A new hand can only be started when the game is in '{nameof(Phases.WaitingToStart)}' " +
				          $"or '{nameof(Phases.Complete)}' phase.",
				Code = StartHandErrorCode.InvalidGameState
			};
		}

		ProcessPendingLeaveRequests(game, now);
		ApplyPendingChips(game);
		AutoSitOutPlayersWithNoChips(game);

		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
			             !gp.IsSittingOut &&
			             gp.ChipStack > 0)
			.DistinctBy(gp => gp.Id)
			.ToList();

		if (eligiblePlayers.Count < 2)
		{
			return new StartHandError
			{
				Message = "Not enough eligible players to start a new hand. At least 2 players with chips are required.",
				Code = StartHandErrorCode.NotEnoughPlayers
			};
		}

		ResetPlayerStates(game);
		await RemovePreviousHandCardsAsync(game, cancellationToken);
		await CompleteOpenBettingRoundsAsync(game, now, cancellationToken);

		var mainPot = new Pot
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber + 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			IsAwarded = false,
			CreatedAt = now
		};

		context.Pots.Add(mainPot);

		var flowHandler = flowHandlerFactory.GetHandler("HOLDEM");

		game.CurrentHandNumber++;
		game.CurrentPhase = flowHandler.GetInitialPhase(game);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;
		game.StartedAt ??= now;

		await flowHandler.OnHandStartingAsync(game, cancellationToken);
		await context.SaveChangesAsync(cancellationToken);

		await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);

		logger.LogInformation(
			"Started Hold'Em hand {HandNumber} for game {GameId} in phase {Phase} with {PlayerCount} eligible players",
			game.CurrentHandNumber,
			game.Id,
			game.CurrentPhase,
			eligiblePlayers.Count);

		return new StartHandSuccessful
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			CurrentPhase = game.CurrentPhase,
			ActivePlayerCount = eligiblePlayers.Count
		};
	}

	private static void ProcessPendingLeaveRequests(Game game, DateTimeOffset now)
	{
		var playersLeaving = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
			.ToList();

		foreach (var player in playersLeaving)
		{
			player.Status = GamePlayerStatus.Left;
			player.LeftAt = now;
			player.FinalChipCount = player.ChipStack;
			player.IsSittingOut = true;
		}
	}

	private static void ApplyPendingChips(Game game)
	{
		var playersWithPendingChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			player.ChipStack += player.PendingChipsToAdd;
			player.PendingChipsToAdd = 0;
		}
	}

	private static void AutoSitOutPlayersWithNoChips(Game game)
	{
		var playersWithNoChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
			             !gp.IsSittingOut &&
			             gp.ChipStack <= 0)
			.ToList();

		foreach (var player in playersWithNoChips)
		{
			player.IsSittingOut = true;
		}
	}

	private static void ResetPlayerStates(Game game)
	{
		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.HasFolded = gamePlayer.IsSittingOut;
			gamePlayer.VariantState = null;
		}
	}

	private async Task RemovePreviousHandCardsAsync(Game game, CancellationToken cancellationToken)
	{
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			context.GameCards.RemoveRange(existingCards);
		}
	}

	private async Task CompleteOpenBettingRoundsAsync(Game game, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var incompleteBettingRounds = await context.BettingRounds
			.Where(br => br.GameId == game.Id && !br.IsComplete)
			.ToListAsync(cancellationToken);

		foreach (var round in incompleteBettingRounds)
		{
			round.IsComplete = true;
			round.CompletedAt = now;
		}
	}
}
