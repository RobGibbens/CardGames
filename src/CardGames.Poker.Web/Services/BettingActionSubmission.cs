namespace CardGames.Poker.Web.Services;

/// <summary>
///     Tracks a betting-action click so the action panel stays locked until that action
///     is reflected in table state. A rejected request releases the lock immediately.
/// </summary>
public sealed class BettingActionSubmission
{
	private enum Phase
	{
		Idle,
		Submitting,
		Accepted
	}

	private Phase _phase = Phase.Idle;
	private ulong _versionAtSubmit;
	private int? _nextPlayerIndex;

	public bool IsLocked => _phase != Phase.Idle;

	/// <summary>
	///     Starts a submission. Returns false when a submission is already in flight or accepted,
	///     so a second click cannot send another action.
	/// </summary>
	public bool TryBegin(ulong stateVersion)
	{
		if (_phase != Phase.Idle)
			return false;

		_phase = Phase.Submitting;
		_versionAtSubmit = stateVersion;
		_nextPlayerIndex = null;
		return true;
	}

	/// <summary>
	///     The request failed or never changed the hand. The panel must accept another action.
	/// </summary>
	public void Reject()
	{
		if (_phase == Phase.Submitting)
			_phase = Phase.Idle;
	}

	/// <summary>
	///     The server accepted the action. Stay locked until table state catches up.
	/// </summary>
	/// <param name="nextPlayerIndex">Seat the server says must act next, or null when the round is over.</param>
	public void Accept(int? nextPlayerIndex)
	{
		if (_phase != Phase.Submitting)
			return;

		_phase = Phase.Accepted;
		_nextPlayerIndex = nextPlayerIndex;
	}

	/// <summary>
	///     Reconciles an authoritative snapshot. In-flight requests stay locked so a snapshot that
	///     arrives before the response cannot re-enable the buttons. An accepted action unlocks
	///     when the panel is gone, or when a newer snapshot shows the next player the server named.
	/// </summary>
	public void Observe(ulong stateVersion, bool actionPanelVisible, int currentActorSeatIndex)
	{
		if (_phase != Phase.Accepted)
			return;

		if (!actionPanelVisible)
		{
			_phase = Phase.Idle;
			return;
		}

		// A newer snapshot is a new decision only when it shows the player the server
		// named. A completed round reports no next player, so the panel has to close first.
		if (stateVersion > _versionAtSubmit
		    && _nextPlayerIndex is >= 0
		    && currentActorSeatIndex == _nextPlayerIndex)
		{
			_phase = Phase.Idle;
		}
	}

	public void Reset()
	{
		_phase = Phase.Idle;
		_versionAtSubmit = 0;
		_nextPlayerIndex = null;
	}
}
