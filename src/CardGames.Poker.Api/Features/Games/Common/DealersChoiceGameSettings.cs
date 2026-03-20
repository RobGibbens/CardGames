using System.Text.Json;
using System.Text.Json.Nodes;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Common;

public static class DealersChoiceGameSettings
{
	private const string AllowedGameCodesKey = "allowedDealerChoiceGameCodes";

	public static IReadOnlyList<string> NormalizeGameCodes(IEnumerable<string>? gameCodes)
	{
		return gameCodes?
			.Select(code => code?.Trim())
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Select(code => code!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
			.ToArray() ?? [];
	}

	public static IReadOnlyList<string>? GetAllowedGameCodes(Game game)
	{
		return GetAllowedGameCodes(game.GameSettings);
	}

	public static IReadOnlyList<string>? GetAllowedGameCodes(string? gameSettings)
	{
		var settings = ParseSettings(gameSettings);
		var allowedCodes = settings[AllowedGameCodesKey]?.Deserialize<string[]>();
		var normalizedCodes = NormalizeGameCodes(allowedCodes);

		return normalizedCodes.Count > 0 ? normalizedCodes : null;
	}

	public static bool IsGameCodeAllowed(Game game, string? gameCode)
	{
		var allowedGameCodes = GetAllowedGameCodes(game);
		if (allowedGameCodes is null)
		{
			return true;
		}

		if (string.IsNullOrWhiteSpace(gameCode))
		{
			return false;
		}

		return allowedGameCodes.Contains(gameCode, StringComparer.OrdinalIgnoreCase);
	}

	public static void SaveAllowedGameCodes(Game game, IEnumerable<string>? gameCodes)
	{
		var settings = ParseSettings(game.GameSettings);
		var normalizedCodes = NormalizeGameCodes(gameCodes);

		if (normalizedCodes.Count == 0)
		{
			settings.Remove(AllowedGameCodesKey);
		}
		else
		{
			settings[AllowedGameCodesKey] = JsonSerializer.SerializeToNode(normalizedCodes);
		}

		game.GameSettings = settings.Count > 0 ? settings.ToJsonString() : null;
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