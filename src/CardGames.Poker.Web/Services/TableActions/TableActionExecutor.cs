using Microsoft.Extensions.Logging;

namespace CardGames.Poker.Web.Services.TableActions;

/// <summary>
/// Executes table actions with a consistent lifecycle: re-entrancy guarding,
/// loading-state handling, UI refresh, structured error logging, and
/// success/failure notifications.
/// </summary>
/// <remarks>
/// The executor is intentionally UI-framework agnostic. The owning component
/// supplies a refresh callback (e.g. <c>InvokeAsync(StateHasChanged)</c>) and a
/// notification callback (e.g. a toast helper), keeping all the repetitive
/// orchestration in one place.
/// </remarks>
public sealed class TableActionExecutor
{
    private readonly ILogger _logger;
    private readonly Func<Task> _refreshAsync;
    private readonly Func<string, string, int, Task> _notifyAsync;

    /// <param name="logger">Logger used for warnings (failures) and errors (exceptions).</param>
    /// <param name="refreshAsync">Refreshes the UI (typically <c>InvokeAsync(StateHasChanged)</c>).</param>
    /// <param name="notifyAsync">Shows a notification: message, type, duration in milliseconds.</param>
    public TableActionExecutor(
        ILogger logger,
        Func<Task> refreshAsync,
        Func<string, string, int, Task> notifyAsync)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _refreshAsync = refreshAsync ?? throw new ArgumentNullException(nameof(refreshAsync));
        _notifyAsync = notifyAsync ?? throw new ArgumentNullException(nameof(notifyAsync));
    }

    /// <summary>Executes an action whose result content is not needed on success.</summary>
    public Task ExecuteAsync(Func<Task<TableActionResult>> operation, TableActionOptions options)
        => RunCoreAsync(operation, onSuccess: null, options);

    /// <summary>
    /// Executes an action and invokes <paramref name="onSuccess"/> with the typed
    /// result when it succeeds (for handlers that mutate state from the response).
    /// </summary>
    public Task ExecuteAsync<T>(
        Func<Task<TableActionResult<T>>> operation,
        Func<TableActionResult<T>, Task>? onSuccess,
        TableActionOptions options)
        => RunCoreAsync(
            async () => await operation().ConfigureAwait(false),
            onSuccess is null ? null : result => onSuccess((TableActionResult<T>)result),
            options);

    private async Task RunCoreAsync(
        Func<Task<TableActionResult>> operation,
        Func<TableActionResult, Task>? onSuccess,
        TableActionOptions options)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);

        if (options.IsBusy?.Invoke() == true)
        {
            return;
        }

        options.SetBusy?.Invoke(true);

        if (options.RefreshOnStart)
        {
            await _refreshAsync().ConfigureAwait(false);
        }

        try
        {
            var result = await operation().ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (onSuccess is not null)
                {
                    await onSuccess(result).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(options.SuccessMessage))
                {
                    await _notifyAsync(options.SuccessMessage!, options.SuccessToastType, options.ToastDurationMs)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogWarning("{ActionName} failed: {Error}", options.ActionName, result.Error);

                if (options.ShowFailureToast)
                {
                    var message = ResolveFailureMessage(options, result.Error);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        await _notifyAsync(message!, options.FailureToastType, options.ToastDurationMs)
                            .ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ActionName}", options.ActionName);

            if (options.ShowExceptionToast)
            {
                var message = options.FailureMessage
                    ?? options.FailureFallbackMessage
                    ?? "Something went wrong. Please try again.";
                await _notifyAsync(message, options.FailureToastType, options.ToastDurationMs)
                    .ConfigureAwait(false);
            }

            if (options.RethrowOnException)
            {
                throw;
            }
        }
        finally
        {
            options.SetBusy?.Invoke(false);

            if (options.RefreshOnComplete)
            {
                await _refreshAsync().ConfigureAwait(false);
            }
        }
    }

    private static string? ResolveFailureMessage(TableActionOptions options, string? error)
    {
        if (!string.IsNullOrWhiteSpace(options.FailureMessage))
        {
            return options.FailureMessage;
        }

        return string.IsNullOrWhiteSpace(error) ? options.FailureFallbackMessage : error;
    }
}
