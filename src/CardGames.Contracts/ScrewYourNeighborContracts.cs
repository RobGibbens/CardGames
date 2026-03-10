using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Request to record a keep or trade decision in Screw Your Neighbor.
/// </summary>
public partial record KeepOrTradeRequest
{
	[JsonConstructor]
	public KeepOrTradeRequest(string decision, System.Guid playerId)
	{
		PlayerId = playerId;
		Decision = decision;
	}

	[JsonPropertyName("playerId")]
	public System.Guid PlayerId { get; init; }

	[JsonPropertyName("decision")]
	public string Decision { get; init; }
}

/// <summary>
/// Successful response from a keep or trade decision in Screw Your Neighbor.
/// </summary>
public partial record KeepOrTradeSuccessful
{
	[JsonConstructor]
	public KeepOrTradeSuccessful(
		System.Guid gameId,
		System.Guid playerId,
		string decision,
		bool didTrade,
		bool wasBlocked,
		string? nextPhase,
		int? nextPlayerSeatIndex)
	{
		GameId = gameId;
		PlayerId = playerId;
		Decision = decision;
		DidTrade = didTrade;
		WasBlocked = wasBlocked;
		NextPhase = nextPhase;
		NextPlayerSeatIndex = nextPlayerSeatIndex;
	}

	[JsonPropertyName("gameId")]
	public System.Guid GameId { get; init; }

	[JsonPropertyName("playerId")]
	public System.Guid PlayerId { get; init; }

	[JsonPropertyName("decision")]
	public string Decision { get; init; }

	[JsonPropertyName("didTrade")]
	public bool DidTrade { get; init; }

	[JsonPropertyName("wasBlocked")]
	public bool WasBlocked { get; init; }

	[JsonPropertyName("nextPhase")]
	public string? NextPhase { get; init; }

	[JsonPropertyName("nextPlayerSeatIndex")]
	public int? NextPlayerSeatIndex { get; init; }
}
