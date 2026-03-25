using System.Text.Json;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Tollbooth;

/// <summary>
/// Manages Tollbooth-specific variant state stored on Game and GamePlayer entities.
/// </summary>
internal static class TollboothVariantState
{
	/// <summary>
	/// Gets the previous betting street from game-level variant state so the flow handler
	/// can determine which betting street follows the current TollboothOffer.
	/// </summary>
	public static string? GetPreviousBettingStreet(Game game)
	{
		if (string.IsNullOrWhiteSpace(game.GameSettings))
		{
			return null;
		}

		try
		{
			var state = JsonSerializer.Deserialize<GameState>(game.GameSettings);
			return state?.PreviousBettingStreet;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>
	/// Records which betting street just completed so the TollboothOffer → next betting street
	/// transition can be resolved deterministically.
	/// </summary>
	public static void SetPreviousBettingStreet(Game game, string street)
	{
		var state = TryDeserializeGameState(game) ?? new GameState();
		state = state with { PreviousBettingStreet = street };
		game.GameSettings = JsonSerializer.Serialize(state);
	}

	private static GameState? TryDeserializeGameState(Game game)
	{
		if (string.IsNullOrWhiteSpace(game.GameSettings))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<GameState>(game.GameSettings);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private sealed record GameState
	{
		public string? PreviousBettingStreet { get; init; }
	}
}
