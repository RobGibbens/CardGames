namespace CardGames.Poker.Api.Contracts;

/// <summary>
///     Request to declare an Ace as high or low during In-Between.
/// </summary>
public record InBetweenAceChoiceRequest
{
	[JsonConstructor]
	public InBetweenAceChoiceRequest(Guid playerId, bool aceIsHigh)
	{
		PlayerId = playerId;
		AceIsHigh = aceIsHigh;
	}

	[JsonPropertyName("playerId")] public Guid PlayerId { get; init; }

	[JsonPropertyName("aceIsHigh")] public bool AceIsHigh { get; init; }
}

/// <summary>
///     Successful response from an ace choice in In-Between.
/// </summary>
public record InBetweenAceChoiceSuccessful
{
	[JsonConstructor]
	public InBetweenAceChoiceSuccessful(
		Guid gameId,
		Guid playerId,
		bool aceIsHigh,
		string? nextSubPhase)
	{
		GameId = gameId;
		PlayerId = playerId;
		AceIsHigh = aceIsHigh;
		NextSubPhase = nextSubPhase;
	}

	[JsonPropertyName("gameId")] public Guid GameId { get; init; }

	[JsonPropertyName("playerId")] public Guid PlayerId { get; init; }

	[JsonPropertyName("aceIsHigh")] public bool AceIsHigh { get; init; }

	[JsonPropertyName("nextSubPhase")] public string? NextSubPhase { get; init; }
}

/// <summary>
///     Request to place a bet (or pass with amount 0) in In-Between.
/// </summary>
public record InBetweenPlaceBetRequest
{
	[JsonConstructor]
	public InBetweenPlaceBetRequest(Guid playerId, int amount)
	{
		PlayerId = playerId;
		Amount = amount;
	}

	[JsonPropertyName("playerId")] public Guid PlayerId { get; init; }

	[JsonPropertyName("amount")] public int Amount { get; init; }
}

/// <summary>
///     Successful response from a bet/pass in In-Between.
/// </summary>
public record InBetweenPlaceBetSuccessful
{
	[JsonConstructor]
	public InBetweenPlaceBetSuccessful(
		Guid gameId,
		Guid playerId,
		int amount,
		string turnResult,
		string? description,
		string? nextPhase,
		int? nextPlayerSeatIndex,
		int potAmount)
	{
		GameId = gameId;
		PlayerId = playerId;
		Amount = amount;
		TurnResult = turnResult;
		Description = description;
		NextPhase = nextPhase;
		NextPlayerSeatIndex = nextPlayerSeatIndex;
		PotAmount = potAmount;
	}

	[JsonPropertyName("gameId")] public Guid GameId { get; init; }

	[JsonPropertyName("playerId")] public Guid PlayerId { get; init; }

	[JsonPropertyName("amount")] public int Amount { get; init; }

	[JsonPropertyName("turnResult")] public string TurnResult { get; init; }

	[JsonPropertyName("description")] public string? Description { get; init; }

	[JsonPropertyName("nextPhase")] public string? NextPhase { get; init; }

	[JsonPropertyName("nextPlayerSeatIndex")]
	public int? NextPlayerSeatIndex { get; init; }

	[JsonPropertyName("potAmount")] public int PotAmount { get; init; }
}