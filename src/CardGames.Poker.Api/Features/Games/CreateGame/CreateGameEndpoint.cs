using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.CreateGame;

/// <summary>
/// Wolverine HTTP endpoint for creating a new poker game.
/// </summary>
public static class CreateGameEndpoint
{
	/// <summary>
	/// Creates a new poker game and starts an event stream.
	/// </summary>
	[WolverinePost("/api/v1/games")]
	[EndpointName("CreateGame")]
	public static async Task<CreateGameResponse> Post(
		CreateGameRequest request,
		IDocumentSession session,
		CancellationToken cancellationToken)
	{
		var gameId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;

		// Build configuration from request or use defaults
		var defaultConfig = GameConfiguration.DefaultFiveCardDraw;
		var configuration = new GameConfiguration(
			Ante: request.Configuration?.Ante ?? defaultConfig.Ante,
			MinBet: request.Configuration?.MinBet ?? defaultConfig.MinBet,
			StartingChips: request.Configuration?.StartingChips ?? defaultConfig.StartingChips,
			MaxPlayers: request.Configuration?.MaxPlayers ?? defaultConfig.MaxPlayers
		);

		// Create the domain event
		var gameCreatedEvent = new GameCreated(
			gameId,
			request.GameType,
			configuration,
			createdAt
		);

		// Start the event stream for this game
		session.Events.StartStream<PokerGameAggregate>(gameId, gameCreatedEvent);
		await session.SaveChangesAsync(cancellationToken);

		return new CreateGameResponse(
			gameId,
			request.GameType,
			GameStatus.WaitingForPlayers,
			configuration,
			createdAt
		);
	}
}