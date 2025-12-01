using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CardGames.Poker.Api.Features.Chat;

/// <summary>
/// Validates chat messages for inappropriate content and rate limiting.
/// </summary>
public interface IChatMessageValidator
{
    /// <summary>
    /// Validates a chat message.
    /// </summary>
    /// <param name="playerName">The sender's name.</param>
    /// <param name="content">The message content.</param>
    /// <returns>A validation result with success status and optional error message.</returns>
    (bool IsValid, string? Error) Validate(string playerName, string content);
}

/// <summary>
/// Default implementation of chat message validation.
/// </summary>
public class ChatMessageValidator : IChatMessageValidator
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _messageTimestamps = new();
    private readonly int _maxMessageLength = 500;
    private readonly int _minMessageLength = 1;
    private readonly int _maxMessagesPerMinute = 10;
    private readonly int _maxMessagesPerSecond = 2;
    private readonly TimeSpan _rateLimitWindow = TimeSpan.FromMinutes(1);

    // Inappropriate words list for basic content filtering
    // In production, consider using a more comprehensive third-party content moderation service
    private static readonly HashSet<string> InappropriateWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // This list is intentionally minimal - poker tables should allow most casual conversation
        // Severe inappropriate content should be handled by a dedicated moderation service
    };

    // Pattern to detect spam-like repeated characters
    private static readonly Regex RepeatedCharsPattern = new(@"(.)\1{4,}", RegexOptions.Compiled);

    /// <inheritdoc />
    public (bool IsValid, string? Error) Validate(string playerName, string content)
    {
        // Check for empty content
        if (string.IsNullOrWhiteSpace(content))
        {
            return (false, "Message cannot be empty.");
        }

        // Check minimum length
        var trimmedContent = content.Trim();
        if (trimmedContent.Length < _minMessageLength)
        {
            return (false, "Message is too short.");
        }

        // Check maximum length
        if (trimmedContent.Length > _maxMessageLength)
        {
            return (false, $"Message exceeds maximum length of {_maxMessageLength} characters.");
        }

        // Check rate limiting
        if (!CheckRateLimit(playerName))
        {
            return (false, "You are sending messages too quickly. Please wait a moment.");
        }

        // Check for inappropriate content
        var contentLower = trimmedContent.ToLowerInvariant();
        foreach (var word in InappropriateWords)
        {
            if (contentLower.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Message contains inappropriate content.");
            }
        }

        // Check for excessive caps (more than 10 chars and mostly uppercase)
        if (trimmedContent.Length > 10)
        {
            var upperCount = trimmedContent.Count(char.IsUpper);
            var letterCount = trimmedContent.Count(char.IsLetter);
            if (letterCount > 0 && (double)upperCount / letterCount > 0.7)
            {
                return (false, "Please avoid excessive use of capital letters.");
            }
        }

        // Check for spam-like repeated characters
        if (RepeatedCharsPattern.IsMatch(trimmedContent))
        {
            return (false, "Message contains too many repeated characters.");
        }

        return (true, null);
    }

    private bool CheckRateLimit(string playerName)
    {
        var now = DateTime.UtcNow;
        var timestamps = _messageTimestamps.GetOrAdd(playerName, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            // Remove old timestamps outside the window
            while (timestamps.Count > 0 && now - timestamps.Peek() > _rateLimitWindow)
            {
                timestamps.Dequeue();
            }

            // Check messages per minute
            if (timestamps.Count >= _maxMessagesPerMinute)
            {
                return false;
            }

            // Check messages per second (burst protection)
            var recentMessages = timestamps.Count(t => now - t < TimeSpan.FromSeconds(1));
            if (recentMessages >= _maxMessagesPerSecond)
            {
                return false;
            }

            // Add current timestamp
            timestamps.Enqueue(now);
            return true;
        }
    }
}
