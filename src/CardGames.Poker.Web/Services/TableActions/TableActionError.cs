using System;
using System.Text.Json;
using Refit;

namespace CardGames.Poker.Web.Services.TableActions;

/// <summary>
/// Normalizes the various error shapes returned by the table APIs (raw strings,
/// JSON <c>{ "message": "..." }</c> bodies and Refit <see cref="ApiException"/>s)
/// into user-facing messages.
/// </summary>
/// <remarks>
/// This consolidates the error-extraction logic that was previously duplicated
/// across the table page so every action handler reports failures consistently.
/// </remarks>
public static class TableActionError
{
    private const string DefaultMessage = "An error occurred.";

    /// <summary>
    /// Extracts the <c>message</c>/<c>Message</c> property from a JSON error body.
    /// Returns <see langword="null"/> for empty input and the raw body when it is
    /// not JSON or has no message property.
    /// </summary>
    public static string? FromBody(string? errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return null;
        }

        // Fast path: if it doesn't look like JSON, return as-is.
        if (!errorBody.TrimStart().StartsWith('{'))
        {
            return errorBody;
        }

        return TryReadJsonMessage(errorBody) ?? errorBody;
    }

    /// <summary>
    /// Extracts a user-facing message from a Refit <see cref="ApiException"/>,
    /// falling back to <paramref name="fallback"/> when no detail is available.
    /// </summary>
    public static string FromException(ApiException? exception, string fallback = "An unknown error occurred.")
    {
        if (exception is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(exception.Content))
        {
            return exception.Message ?? DefaultMessage;
        }

        // Fast path: if the content isn't JSON, return it verbatim.
        if (!exception.Content!.TrimStart().StartsWith('{'))
        {
            return exception.Content;
        }

        var message = TryReadJsonMessage(exception.Content);
        if (message is not null)
        {
            return message;
        }

        // The body looked like JSON but had no message property; surface the raw content.
        return exception.Content;
    }

    private static string? TryReadJsonMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }

            if (document.RootElement.TryGetProperty("Message", out var upperMessageElement))
            {
                return upperMessageElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Not valid JSON — caller decides on the fallback.
        }

        return null;
    }
}
