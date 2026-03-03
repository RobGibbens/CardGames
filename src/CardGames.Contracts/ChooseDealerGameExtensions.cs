using System.Text.Json.Serialization;
using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the ChooseDealerGame endpoint (added in Phase 2).
/// Will be auto-generated in Phase 8 when Refit is regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/{gameId}/choose-game")]
	Task<IApiResponse<ChooseDealerGameSuccessful>> ChooseDealerGameAsync(Guid gameId, [Body] ChooseDealerGameRequest body, CancellationToken cancellationToken = default);
}

public record ChooseDealerGameRequest(
	[property: JsonPropertyName("gameTypeCode")] string GameTypeCode,
	[property: JsonPropertyName("ante")] int Ante,
	[property: JsonPropertyName("minBet")] int MinBet
);

public record ChooseDealerGameSuccessful
{
	[JsonPropertyName("gameId")]
	public Guid GameId { get; init; }

	[JsonPropertyName("gameTypeCode")]
	public string GameTypeCode { get; init; } = "";

	[JsonPropertyName("gameTypeName")]
	public string GameTypeName { get; init; } = "";

	[JsonPropertyName("handNumber")]
	public int HandNumber { get; init; }

	[JsonPropertyName("ante")]
	public int Ante { get; init; }

	[JsonPropertyName("minBet")]
	public int MinBet { get; init; }
}
