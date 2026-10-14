using MediatR;
using OneOf;
using GenericPerformShowdownErrorCode =
	CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownErrorCode;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;

/// <summary>
///     Forwards Hold the Baseball showdown to the generic showdown handler.
///     The generic handler evaluates with <c>HoldTheBaseballHandEvaluator</c> and pays
///     contribution layers. This type only maps that result.
/// </summary>
public class PerformShowdownCommandHandler(IMediator mediator)
	: IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
	public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
		PerformShowdownCommand command,
		CancellationToken cancellationToken)
	{
		var result = await mediator.Send(
			new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(
				command.GameId),
			cancellationToken);

		return result.Match<OneOf<PerformShowdownSuccessful, PerformShowdownError>>(
			success => new PerformShowdownSuccessful
			{
				GameId = success.GameId,
				WonByFold = success.WonByFold,
				CurrentPhase = success.CurrentPhase,
				Payouts = success.Payouts,
				PlayerHands = success.PlayerHands.Select(hand => new ShowdownPlayerHand
				{
					PlayerName = hand.PlayerName,
					PlayerFirstName = hand.PlayerFirstName,
					Cards = hand.Cards.Select(card => new ShowdownCard
					{
						Suit = card.Suit,
						Symbol = card.Symbol
					}).ToList(),
					HandType = hand.HandType,
					HandDescription = hand.HandDescription,
					HandStrength = hand.HandStrength,
					IsWinner = hand.IsWinner,
					AmountWon = hand.AmountWon,
					BestCardIndexes = hand.BestCardIndexes
				}).ToList()
			},
			error => new PerformShowdownError
			{
				Message = error.Message,
				Code = error.Code == GenericPerformShowdownErrorCode.GameNotFound
					? PerformShowdownErrorCode.GameNotFound
					: PerformShowdownErrorCode.InvalidGameState
			});
	}
}
