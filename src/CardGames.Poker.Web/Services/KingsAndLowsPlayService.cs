using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Gameplay service for Kings and Lows poker variant.
/// Note: Kings and Lows has a unique flow without standard betting actions.
/// This service provides stub implementations that should not be called.
/// </summary>
public class KingsAndLowsPlayService : IGamePlayService
{
	private readonly IKingsAndLowsApi _api;

	public KingsAndLowsPlayService(IKingsAndLowsApi api)
	{
		_api = api ?? throw new ArgumentNullException(nameof(api));
	}

	/// <inheritdoc />
	public bool SupportsDraw => true;

	/// <inheritdoc />
	public string GameTypeCode => "KINGSANDLOWS";

	/// <inheritdoc />
	public Task<ProcessBettingActionSuccessful> ProcessBettingActionAsync(
		Guid gameId,
		ProcessBettingActionRequest request,
		CancellationToken cancellationToken = default)
	{
		// Kings and Lows doesn't use standard betting actions
		throw new NotSupportedException("Kings and Lows does not use standard betting actions");
	}

	/// <inheritdoc />
	public Task<ProcessDrawSuccessful> ProcessDrawAsync(
		Guid gameId,
		ProcessDrawRequest request,
		CancellationToken cancellationToken = default)
	{
		// Kings and Lows has its own draw API (DrawCardsAsync) which is called directly from the UI
		throw new NotSupportedException("Kings and Lows uses a custom draw API (DrawCardsAsync)");
	}
}
