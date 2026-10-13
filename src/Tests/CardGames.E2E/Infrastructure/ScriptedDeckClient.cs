using System.Net.Http.Json;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Posts a deck script to the development testing API before a hand starts.
/// </summary>
public static class ScriptedDeckClient
{
	public static Task ScriptHoldEmAsync(
		string apiBaseUrl,
		Guid gameId,
		int dealerSeat,
		IReadOnlyDictionary<int, IReadOnlyList<string>> holeCardsBySeat,
		IReadOnlyList<string> board,
		CancellationToken cancellationToken = default)
	{
		var cards = HoldEmDeckScript.Build(dealerSeat, holeCardsBySeat, board);
		return SetAsync(apiBaseUrl, gameId, cards, cancellationToken);
	}

	public static Task ScriptRedRiverAsync(
		string apiBaseUrl,
		Guid gameId,
		int dealerSeat,
		IReadOnlyDictionary<int, IReadOnlyList<string>> holeCardsBySeat,
		IReadOnlyList<string> board,
		CancellationToken cancellationToken = default)
	{
		var cards = RedRiverDeckScript.Build(dealerSeat, holeCardsBySeat, board);
		return SetAsync(apiBaseUrl, gameId, cards, cancellationToken);
	}

	public static Task ScriptNebraskaAsync(
		string apiBaseUrl,
		Guid gameId,
		int dealerSeat,
		IReadOnlyDictionary<int, IReadOnlyList<string>> holeCardsBySeat,
		IReadOnlyList<string> board,
		CancellationToken cancellationToken = default)
	{
		var cards = NebraskaDeckScript.Build(dealerSeat, holeCardsBySeat, board);
		return SetAsync(apiBaseUrl, gameId, cards, cancellationToken);
	}

	public static async Task SetAsync(
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<string> cards,
		CancellationToken cancellationToken = default)
	{
		using var handler = new HttpClientHandler
		{
			ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
		};
		using var client = new HttpClient(handler)
		{
			BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/")
		};

		var response = await client.PostAsJsonAsync(
			$"api/v1/testing/games/{gameId}/deck",
			new { cards },
			cancellationToken);

		if (response.IsSuccessStatusCode)
			return;

		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		throw new InvalidOperationException(
			$"Scripted deck request failed ({(int)response.StatusCode}): {body}");
	}
}
