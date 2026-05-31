namespace CardGames.Poker.Web.Services.TableActions;

/// <summary>
/// Configures how a single table action is executed by
/// <see cref="TableActionExecutor"/>: re-entrancy guarding, loading-state
/// toggling, UI refresh timing, and success/failure notifications.
/// </summary>
public sealed class TableActionOptions
{
    /// <summary>
    /// Returns the current "busy" state. When it returns <see langword="true"/>
    /// the action is skipped, preventing double submissions.
    /// </summary>
    public Func<bool>? IsBusy { get; init; }

    /// <summary>Toggles the loading/"busy" flag around the action.</summary>
    public Action<bool>? SetBusy { get; init; }

    /// <summary>Refresh the UI immediately after entering the busy state.</summary>
    public bool RefreshOnStart { get; init; }

    /// <summary>Refresh the UI after the action completes (in the finally block).</summary>
    public bool RefreshOnComplete { get; init; }

    /// <summary>Show a toast when the API reports a failure result.</summary>
    public bool ShowFailureToast { get; init; } = true;

    /// <summary>Show a toast when the action throws an unexpected exception.</summary>
    public bool ShowExceptionToast { get; init; }

    /// <summary>Re-throw unexpected exceptions after logging (and after the finally block).</summary>
    public bool RethrowOnException { get; init; }

    /// <summary>
    /// A fixed failure message that overrides the normalized API error.
    /// </summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// A fallback message used when the API failure does not include any detail.
    /// </summary>
    public string? FailureFallbackMessage { get; init; }

    /// <summary>A toast shown on success. The dynamic success path can use the callback instead.</summary>
    public string? SuccessMessage { get; init; }

    /// <summary>Toast type used for failures (defaults to "error").</summary>
    public string FailureToastType { get; init; } = "error";

    /// <summary>Toast type used for the optional success message (defaults to "info").</summary>
    public string SuccessToastType { get; init; } = "info";

    /// <summary>Duration, in milliseconds, for emitted toasts.</summary>
    public int ToastDurationMs { get; init; } = 4000;

    /// <summary>A human-readable name used in log messages.</summary>
    public string ActionName { get; init; } = "table action";
}
