namespace CardGames.Poker.Web.Services;

/// <summary>
/// A transient toast notification shown on the table page.
/// </summary>
/// <param name="Message">The text displayed in the toast.</param>
/// <param name="Type">The toast severity/style (e.g. "info", "success", "warning", "error").</param>
public sealed record ToastMessage(string Message, string Type);

/// <summary>
/// Circuit-scoped state service that owns the table page's toast notifications.
/// Exposes the current toast list plus a <see cref="ShowAsync"/> command, and raises
/// <see cref="OnChanged"/> whenever the list changes so the component can re-render.
/// Modelled after <see cref="DashboardState"/>.
/// </summary>
public sealed class TablePlayToastState
{
    private readonly object _gate = new();
    private readonly List<ToastMessage> _messages = [];

    /// <summary>
    /// The toast messages currently visible, in insertion order. Returns a stable
    /// snapshot so callers (e.g. component rendering) can enumerate safely while the
    /// list is mutated from background auto-dismiss tasks.
    /// </summary>
    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    /// <summary>
    /// Raised whenever the toast list changes (added or auto-dismissed).
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Shows a toast and schedules its auto-dismissal after <paramref name="durationMs"/>.
    /// </summary>
    /// <param name="message">The text to display.</param>
    /// <param name="type">The toast severity/style.</param>
    /// <param name="durationMs">How long the toast stays visible before auto-dismissal.</param>
    public Task ShowAsync(string message, string type = "info", int durationMs = 4000)
    {
        var toast = new ToastMessage(message, type);
        lock (_gate)
        {
            _messages.Add(toast);
        }
        NotifyChanged();

        // Auto-dismiss after duration.
        _ = Task.Run(async () =>
        {
            await Task.Delay(durationMs);
            lock (_gate)
            {
                _messages.Remove(toast);
            }
            NotifyChanged();
        });

        return Task.CompletedTask;
    }

    private void NotifyChanged() => OnChanged?.Invoke();
}
