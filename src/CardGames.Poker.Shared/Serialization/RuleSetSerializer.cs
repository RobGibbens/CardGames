using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Validation;

namespace CardGames.Poker.Shared.Serialization;

/// <summary>
/// Provides serialization and deserialization for RuleSetDto.
/// </summary>
public static class RuleSetSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a RuleSetDto to JSON.
    /// </summary>
    /// <param name="ruleSet">The ruleset to serialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>JSON string representation of the ruleset.</returns>
    public static string Serialize(RuleSetDto ruleSet, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return JsonSerializer.Serialize(ruleSet, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to a RuleSetDto.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized RuleSetDto.</returns>
    /// <exception cref="ArgumentException">Thrown when the JSON is null or empty.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static RuleSetDto Deserialize(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<RuleSetDto>(json, options ?? DefaultOptions)
            ?? throw new JsonException("Failed to deserialize ruleset.");
    }

    /// <summary>
    /// Deserializes and validates a JSON string to a RuleSetDto.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized and validated RuleSetDto.</returns>
    /// <exception cref="RuleSetValidationException">Thrown when the ruleset is invalid.</exception>
    public static RuleSetDto DeserializeAndValidate(string json, JsonSerializerOptions? options = null)
    {
        var ruleSet = Deserialize(json, options);
        RuleSetValidator.ValidateAndThrow(ruleSet);
        return ruleSet;
    }

    /// <summary>
    /// Tries to deserialize a JSON string to a RuleSetDto.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="ruleSet">The deserialized ruleset, or null if deserialization failed.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>True if deserialization succeeded, false otherwise.</returns>
    public static bool TryDeserialize(string json, out RuleSetDto? ruleSet, JsonSerializerOptions? options = null)
    {
        ruleSet = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            ruleSet = JsonSerializer.Deserialize<RuleSetDto>(json, options ?? DefaultOptions);
            return ruleSet is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to deserialize and validate a JSON string to a RuleSetDto.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="ruleSet">The deserialized ruleset, or null if deserialization or validation failed.</param>
    /// <param name="errors">The validation errors if validation failed.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>True if deserialization and validation succeeded, false otherwise.</returns>
    public static bool TryDeserializeAndValidate(
        string json,
        out RuleSetDto? ruleSet,
        out IReadOnlyList<string> errors,
        JsonSerializerOptions? options = null)
    {
        errors = Array.Empty<string>();

        if (!TryDeserialize(json, out ruleSet, options) || ruleSet is null)
        {
            errors = new[] { "Failed to deserialize JSON." };
            return false;
        }

        errors = RuleSetValidator.Validate(ruleSet);
        if (errors.Count > 0)
        {
            ruleSet = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the default JSON serializer options used by this serializer.
    /// </summary>
    /// <returns>A copy of the default JSON serializer options.</returns>
    public static JsonSerializerOptions GetDefaultOptions()
    {
        return new JsonSerializerOptions(DefaultOptions);
    }
}
