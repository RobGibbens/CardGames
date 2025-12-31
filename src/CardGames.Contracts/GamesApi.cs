//using Refit;
//using System.Text.Json.Serialization;

//namespace CardGames.Poker.Api.Clients;

///// <summary>
///// Generic Games API for retrieving games regardless of type.
///// </summary>
//public partial interface IGamesApi
//{
//	/// <summary>
//	/// Retrieves a specific game by its identifier.
//	/// Works for any game type and returns the game with its type code.
//	/// </summary>
//	/// <param name="gameId">The unique identifier of the game.</param>
//	/// <param name="cancellationToken">Cancellation token.</param>
//	/// <returns>The game details including the game type code.</returns>
//	[Headers("Accept: application/json, application/problem+json")]
//	[Get("/api/v1/games/{gameId}")]
//	Task<IApiResponse<GamesGetGameResponse>> GamesGetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
//}

///// <summary>
///// Response from the generic Games API GetGame endpoint.
///// Includes GameTypeCode for routing to the correct variant-specific API.
///// </summary>
//public record GamesGetGameResponse
//{
//	[JsonPropertyName("id")]
//	public Guid Id { get; init; }

//	[JsonPropertyName("gameTypeId")]
//	public Guid GameTypeId { get; init; }

//	[JsonPropertyName("gameTypeCode")]
//	public string? GameTypeCode { get; init; }

//	[JsonPropertyName("gameTypeName")]
//	public string? GameTypeName { get; init; }

//	[JsonPropertyName("name")]
//	public string? Name { get; init; }

//	[JsonPropertyName("minimumNumberOfPlayers")]
//	public int MinimumNumberOfPlayers { get; init; }

//	[JsonPropertyName("maximumNumberOfPlayers")]
//	public int MaximumNumberOfPlayers { get; init; }

//	[JsonPropertyName("currentPhase")]
//	public string CurrentPhase { get; init; } = string.Empty;

//	[JsonPropertyName("currentPhaseDescription")]
//	public string? CurrentPhaseDescription { get; init; }

//	[JsonPropertyName("currentHandNumber")]
//	public int CurrentHandNumber { get; init; }

//	[JsonPropertyName("dealerPosition")]
//	public int DealerPosition { get; init; }

//	[JsonPropertyName("ante")]
//	public int? Ante { get; init; }

//	[JsonPropertyName("smallBlind")]
//	public int? SmallBlind { get; init; }

//	[JsonPropertyName("bigBlind")]
//	public int? BigBlind { get; init; }

//	[JsonPropertyName("bringIn")]
//	public int? BringIn { get; init; }

//	[JsonPropertyName("smallBet")]
//	public int? SmallBet { get; init; }

//	[JsonPropertyName("bigBet")]
//	public int? BigBet { get; init; }

//	[JsonPropertyName("minBet")]
//	public int? MinBet { get; init; }

//	[JsonPropertyName("gameSettings")]
//	public string? GameSettings { get; init; }

//	[JsonPropertyName("status")]
//	public CardGames.Poker.Api.Contracts.GameStatus Status { get; init; }

//	[JsonPropertyName("currentPlayerIndex")]
//	public int CurrentPlayerIndex { get; init; }

//	[JsonPropertyName("bringInPlayerIndex")]
//	public int BringInPlayerIndex { get; init; }

//	[JsonPropertyName("currentDrawPlayerIndex")]
//	public int CurrentDrawPlayerIndex { get; init; }

//	[JsonPropertyName("randomSeed")]
//	public int? RandomSeed { get; init; }

//	[JsonPropertyName("createdAt")]
//	public DateTimeOffset CreatedAt { get; init; }

//	[JsonPropertyName("updatedAt")]
//	public DateTimeOffset UpdatedAt { get; init; }

//	[JsonPropertyName("startedAt")]
//	public DateTimeOffset? StartedAt { get; init; }

//	[JsonPropertyName("endedAt")]
//	public DateTimeOffset? EndedAt { get; init; }

//	[JsonPropertyName("createdById")]
//	public string? CreatedById { get; init; }

//	[JsonPropertyName("createdByName")]
//	public string? CreatedByName { get; init; }

//	[JsonPropertyName("canContinue")]
//	public bool CanContinue { get; init; }

//	[JsonPropertyName("rowVersion")]
//	public string RowVersion { get; init; } = string.Empty;
//}
