using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Validation;

/// <summary>
/// Validates TableConfigDto configurations for consistency.
/// </summary>
public static class TableConfigValidator
{
    /// <summary>
    /// Minimum allowed seats at a table.
    /// </summary>
    public const int MinSeats = 2;

    /// <summary>
    /// Maximum allowed seats at a table.
    /// </summary>
    public const int MaxSeats = 10;

    /// <summary>
    /// Validates a TableConfigDto and returns a list of validation errors.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>A list of validation error messages. Empty if valid.</returns>
    public static IReadOnlyList<string> Validate(TableConfigDto config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        ValidateSeats(config, errors);
        ValidateStakes(config, errors);
        ValidateBuyIn(config, errors);
        ValidateVariantSeats(config, errors);
        ValidateAnte(config, errors);

        return errors;
    }

    /// <summary>
    /// Validates a TableConfigDto and throws if invalid.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <exception cref="TableConfigValidationException">Thrown when validation fails.</exception>
    public static void ValidateAndThrow(TableConfigDto config)
    {
        var errors = Validate(config);
        if (errors.Count > 0)
        {
            throw new TableConfigValidationException(errors);
        }
    }

    /// <summary>
    /// Checks if a TableConfigDto is valid.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValid(TableConfigDto config)
    {
        return Validate(config).Count == 0;
    }

    private static void ValidateSeats(TableConfigDto config, List<string> errors)
    {
        if (config.MaxSeats < MinSeats || config.MaxSeats > MaxSeats)
        {
            errors.Add($"Number of seats must be between {MinSeats} and {MaxSeats}.");
        }
    }

    private static void ValidateStakes(TableConfigDto config, List<string> errors)
    {
        if (config.SmallBlind <= 0)
        {
            errors.Add("Small blind must be greater than 0.");
        }

        if (config.BigBlind <= 0)
        {
            errors.Add("Big blind must be greater than 0.");
        }

        if (config.BigBlind < config.SmallBlind)
        {
            errors.Add("Big blind must be greater than or equal to small blind.");
        }
    }

    private static void ValidateBuyIn(TableConfigDto config, List<string> errors)
    {
        if (config.MinBuyIn <= 0)
        {
            errors.Add("Minimum buy-in must be greater than 0.");
        }

        if (config.MaxBuyIn <= 0)
        {
            errors.Add("Maximum buy-in must be greater than 0.");
        }

        if (config.MinBuyIn > 0 && config.MaxBuyIn > 0 && config.MaxBuyIn < config.MinBuyIn)
        {
            errors.Add("Maximum buy-in must be greater than or equal to minimum buy-in.");
        }
    }

    private static void ValidateVariantSeats(TableConfigDto config, List<string> errors)
    {
        // Seven Card Stud typically has max 8 players due to card requirements
        if (config.Variant == PokerVariant.SevenCardStud && config.MaxSeats > 8)
        {
            errors.Add("Seven Card Stud supports a maximum of 8 players.");
        }

        // Five Card Draw has max 6 players due to card requirements (52 cards / 8 per player worst case = 6)
        if (config.Variant == PokerVariant.FiveCardDraw && config.MaxSeats > 6)
        {
            errors.Add("Five Card Draw supports a maximum of 6 players.");
        }
    }

    private static void ValidateAnte(TableConfigDto config, List<string> errors)
    {
        if (config.Ante < 0)
        {
            errors.Add("Ante cannot be negative.");
        }
    }
}

/// <summary>
/// Exception thrown when table configuration validation fails.
/// </summary>
public class TableConfigValidationException : Exception
{
    /// <summary>
    /// The validation errors that caused this exception.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates a new TableConfigValidationException with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public TableConfigValidationException(IReadOnlyList<string> errors)
        : base($"Table configuration validation failed: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }
}
