using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Gameplay service for Twos, Jacks, and the Man with the Axe poker variant.
/// </summary>
public class TwosJacksManWithTheAxePlayService : IGamePlayService
{
	private readonly ITwosJacksManWithTheAxeApi _api;

	public TwosJacksManWithTheAxePlayService(ITwosJacksManWithTheAxeApi api)
	{
		_api = api ?? throw new ArgumentNullException(nameof(api));
	}

	/// <inheritdoc />
	public bool SupportsDraw => true;

	/// <inheritdoc />
	public string GameTypeCode => "TWOSJACKSMANWITHTHEAXE";

	/// <inheritdoc />
	public async Task<ProcessBettingActionSuccessful> ProcessBettingActionAsync(
		Guid gameId,
		ProcessBettingActionRequest request,
		CancellationToken cancellationToken = default)
	{
		var response = await _api.TwosJacksManWithTheAxeProcessBettingActionAsync(gameId, request, cancellationToken);
		return response.Content ?? throw new InvalidOperationException("API returned null content");
	}

	/// <inheritdoc />
	public async Task<ProcessDrawSuccessful> ProcessDrawAsync(
		Guid gameId,
		ProcessDrawRequest request,
		CancellationToken cancellationToken = default)
	{
		var response = await _api.TwosJacksManWithTheAxeProcessDrawAsync(gameId, request, cancellationToken);
		return response.Content ?? throw new InvalidOperationException("API returned null content");
	}
}
