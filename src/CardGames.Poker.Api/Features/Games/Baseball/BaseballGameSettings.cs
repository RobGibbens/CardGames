using System.Text.Json;
using System.Text.Json.Nodes;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Baseball;

public static class BaseballGameSettings
{
	private const string BuyCardPriceKey = "buyCardPrice";
	private const string BuyCardOffersKey = "buyCardOffers";
	private const string BuyCardReturnPhaseKey = "buyCardReturnPhase";
	private const string BuyCardReturnActorIndexKey = "buyCardReturnActorIndex";

	public sealed record BuyCardOfferState(
		Guid PlayerId,
		int SeatPosition,
		Guid CardId,
		string Street);

	public sealed record BuyCardState(
		int BuyCardPrice,
		List<BuyCardOfferState> PendingOffers,
		string? ReturnPhase,
		int? ReturnActorIndex);

	public static BuyCardState GetState(Game game, int defaultBuyCardPrice)
	{
		var settings = ParseSettings(game.GameSettings);
		var buyCardPrice = settings[BuyCardPriceKey]?.GetValue<int?>() ?? defaultBuyCardPrice;
		var offers = settings[BuyCardOffersKey]?.Deserialize<List<BuyCardOfferState>>() ?? [];
		var returnPhase = settings[BuyCardReturnPhaseKey]?.GetValue<string?>();
		var returnActorIndex = settings[BuyCardReturnActorIndexKey]?.GetValue<int?>();

		return new BuyCardState(buyCardPrice, offers, returnPhase, returnActorIndex);
	}

	public static void SaveState(Game game, BuyCardState state)
	{
		var settings = ParseSettings(game.GameSettings);

		settings[BuyCardPriceKey] = state.BuyCardPrice;

		if (state.PendingOffers.Count > 0)
		{
			settings[BuyCardOffersKey] = JsonSerializer.SerializeToNode(state.PendingOffers);
		}
		else
		{
			settings.Remove(BuyCardOffersKey);
		}

		if (!string.IsNullOrWhiteSpace(state.ReturnPhase))
		{
			settings[BuyCardReturnPhaseKey] = state.ReturnPhase;
		}
		else
		{
			settings.Remove(BuyCardReturnPhaseKey);
		}

		if (state.ReturnActorIndex.HasValue)
		{
			settings[BuyCardReturnActorIndexKey] = state.ReturnActorIndex.Value;
		}
		else
		{
			settings.Remove(BuyCardReturnActorIndexKey);
		}

		game.GameSettings = settings.ToJsonString();
	}

	private static JsonObject ParseSettings(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new JsonObject();
		}

		try
		{
			return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
		}
		catch (JsonException)
		{
			return new JsonObject();
		}
	}
}
