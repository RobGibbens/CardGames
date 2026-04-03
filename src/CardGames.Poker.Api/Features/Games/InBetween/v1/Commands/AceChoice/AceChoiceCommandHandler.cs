using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using static CardGames.Poker.Api.Features.Games.InBetween.InBetweenVariantState;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;

/// <summary>
/// Handles the <see cref="AceChoiceCommand"/> to process a player's ace high/low declaration.
/// </summary>
public class AceChoiceCommandHandler(CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager)
	: IRequestHandler<AceChoiceCommand, OneOf<AceChoiceSuccessful, AceChoiceError>>
{
	public async Task<OneOf<AceChoiceSuccessful, AceChoiceError>> Handle(
		AceChoiceCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new AceChoiceError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = AceChoiceErrorCode.GameNotFound
			};
		}

		if (!string.Equals(game.CurrentPhase, nameof(Phases.InBetweenTurn), StringComparison.OrdinalIgnoreCase))
		{
			return new AceChoiceError
			{
				Message = $"Cannot make ace choice. Game is in '{game.CurrentPhase}' phase.",
				Code = AceChoiceErrorCode.InvalidPhase
			};
		}

		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new AceChoiceError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = AceChoiceErrorCode.PlayerNotFound
			};
		}

		if (gamePlayer.SeatPosition != game.CurrentPlayerIndex)
		{
			return new AceChoiceError
			{
				Message = "It's not this player's turn.",
				Code = AceChoiceErrorCode.NotPlayersTurn
			};
		}

		var state = GetState(game);
		if (state.SubPhase != TurnSubPhase.AwaitingAceChoice)
		{
			return new AceChoiceError
			{
				Message = "Ace choice is not required at this time.",
				Code = AceChoiceErrorCode.AceChoiceNotRequired
			};
		}

		// Record the ace choice and advance to bet/pass
		state.AceIsHigh = command.AceIsHigh;
		state.SubPhase = TurnSubPhase.AwaitingBetOrPass;
		SetState(game, state);

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);

		if (engineOptions.Value.Enabled)
			await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

		return new AceChoiceSuccessful
		{
			GameId = game.Id,
			PlayerId = command.PlayerId,
			AceIsHigh = command.AceIsHigh,
			NextSubPhase = state.SubPhase.ToString()
		};
	}
}
