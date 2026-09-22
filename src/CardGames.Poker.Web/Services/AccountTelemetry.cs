using System.Diagnostics.Metrics;

namespace CardGames.Poker.Web.Services;

public sealed class AccountTelemetry
{
	public const string MeterName = "CardGames.Poker.Web.Identity";

	private readonly Counter<long> _usersCreated;
	private readonly Counter<long> _signIns;
	private readonly Counter<long> _passwordResets;

	public AccountTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);
		_usersCreated = meter.CreateCounter<long>("identity_users_created_total");
		_signIns = meter.CreateCounter<long>("identity_sign_in_attempts_total");
		_passwordResets = meter.CreateCounter<long>("identity_password_resets_total");
	}

	/// <summary>
	/// Records a successful user registration.
	/// </summary>
	public void RecordUserCreated(string accountType, string provider, bool requiresConfirmation)
	{
		_usersCreated.Add(1,
			new("account_type", Normalize(accountType, "unknown")),
			new("provider", Normalize(provider, "local")),
			new("requires_confirmation", requiresConfirmation));
	}

	/// <summary>
	/// Records a sign-in attempt outcome.
	/// </summary>
	public void RecordSignInAttempt(string method, string outcome, string provider = "local")
	{
		_signIns.Add(1,
			new("method", Normalize(method, "unknown")),
			new("outcome", Normalize(outcome, "unknown")),
			new("provider", Normalize(provider, "local")));
	}

	/// <summary>
	/// Records a password reset lifecycle event.
	/// </summary>
	public void RecordPasswordReset(string stage, string outcome)
	{
		_passwordResets.Add(1,
			new("stage", Normalize(stage, "unknown")),
			new("outcome", Normalize(outcome, "unknown")));
	}

	private static string Normalize(string? value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value)
			? fallback
			: value.Trim().ToLowerInvariant().Replace(' ', '_');
	}
}
