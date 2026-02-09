using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

/// <summary>
/// Command to process a buy-card decision in Baseball poker.
/// </summary>
public record ProcessBuyCardCommand(
	Guid GameId,
	Guid PlayerId,
	bool Accept)
	: IRequest<OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>>, IGameStateChangingCommand;

public record ProcessBuyCardSuccessful
{
	public Guid GameId { get; init; }
	public Guid PlayerId { get; init; }
	public bool Accepted { get; init; }
	public string? CurrentPhase { get; init; }
}

public record ProcessBuyCardError
{
	public required string Message { get; init; }
	public required ProcessBuyCardErrorCode Code { get; init; }
}

public enum ProcessBuyCardErrorCode
{
	GameNotFound,
	InvalidGameState,
	PlayerNotFound,
	NoPendingOffer,
	InsufficientChips
}
