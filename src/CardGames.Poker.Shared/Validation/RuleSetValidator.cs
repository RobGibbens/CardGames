using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Validation;

/// <summary>
/// Validates RuleSetDto configurations for consistency.
/// </summary>
public static class RuleSetValidator
{
    /// <summary>
    /// Current supported schema version.
    /// </summary>
    public const string CurrentSchemaVersion = "1.0";

    /// <summary>
    /// Validates a RuleSetDto and returns a list of validation errors.
    /// </summary>
    /// <param name="ruleSet">The ruleset to validate.</param>
    /// <returns>A list of validation error messages. Empty if valid.</returns>
    public static IReadOnlyList<string> Validate(RuleSetDto ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var errors = new List<string>();

        ValidateSchemaVersion(ruleSet, errors);
        ValidateBasicProperties(ruleSet, errors);
        ValidateDeckComposition(ruleSet.DeckComposition, errors);
        ValidateCardVisibility(ruleSet.CardVisibility, ruleSet.HoleCardRules, errors);
        ValidateBettingRounds(ruleSet.BettingRounds, errors);
        ValidateHoleCardRules(ruleSet.HoleCardRules, errors);
        ValidateCommunityCardRules(ruleSet.CommunityCardRules, ruleSet.HoleCardRules, errors);
        ValidateAnteBlindRules(ruleSet.AnteBlindRules, errors);
        ValidateWildcardRules(ruleSet.WildcardRules, errors);
        ValidateHiLoRules(ruleSet.HiLoRules, errors);
        ValidateHandSizeConsistency(ruleSet, errors);

        return errors;
    }

    /// <summary>
    /// Validates a RuleSetDto and throws if invalid.
    /// </summary>
    /// <param name="ruleSet">The ruleset to validate.</param>
    /// <exception cref="RuleSetValidationException">Thrown when validation fails.</exception>
    public static void ValidateAndThrow(RuleSetDto ruleSet)
    {
        var errors = Validate(ruleSet);
        if (errors.Count > 0)
        {
            throw new RuleSetValidationException(errors);
        }
    }

    /// <summary>
    /// Checks if a RuleSetDto is valid.
    /// </summary>
    /// <param name="ruleSet">The ruleset to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValid(RuleSetDto ruleSet)
    {
        return Validate(ruleSet).Count == 0;
    }

    private static void ValidateSchemaVersion(RuleSetDto ruleSet, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(ruleSet.SchemaVersion))
        {
            errors.Add("SchemaVersion is required.");
        }
        else if (!IsCompatibleSchemaVersion(ruleSet.SchemaVersion))
        {
            errors.Add($"SchemaVersion '{ruleSet.SchemaVersion}' is not supported. Current version is '{CurrentSchemaVersion}'.");
        }
    }

    private static bool IsCompatibleSchemaVersion(string version)
    {
        // Support 1.x versions
        return version.StartsWith("1.", StringComparison.Ordinal);
    }

    private static void ValidateBasicProperties(RuleSetDto ruleSet, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(ruleSet.Id))
        {
            errors.Add("Id is required.");
        }

        if (string.IsNullOrWhiteSpace(ruleSet.Name))
        {
            errors.Add("Name is required.");
        }
    }

    private static void ValidateDeckComposition(DeckCompositionDto deck, List<string> errors)
    {
        if (deck.NumberOfDecks < 1)
        {
            errors.Add("NumberOfDecks must be at least 1.");
        }

        if (deck.DeckType == DeckType.Custom)
        {
            if (deck.IncludedCards is null || deck.IncludedCards.Count == 0)
            {
                errors.Add("Custom deck type requires IncludedCards to be specified.");
            }
        }

        if (deck.ExcludedCards is not null && deck.IncludedCards is not null)
        {
            var overlap = deck.ExcludedCards.Intersect(deck.IncludedCards).ToList();
            if (overlap.Count > 0)
            {
                errors.Add($"Cards cannot be both excluded and included: {string.Join(", ", overlap)}.");
            }
        }
    }

    private static void ValidateCardVisibility(CardVisibilityDto visibility, HoleCardRulesDto holeRules, List<string> errors)
    {
        // For stud games with face-up/face-down indices
        if (visibility.FaceUpIndices is not null && visibility.FaceDownIndices is not null)
        {
            var allIndices = visibility.FaceUpIndices.Concat(visibility.FaceDownIndices).ToList();
            if (allIndices.Count != allIndices.Distinct().Count())
            {
                errors.Add("Card indices cannot appear in both FaceUpIndices and FaceDownIndices.");
            }

            var totalCards = holeRules.Count;
            var maxIndex = allIndices.Count > 0 ? allIndices.Max() : -1;
            if (maxIndex >= totalCards)
            {
                errors.Add($"Card visibility index {maxIndex} exceeds hole card count of {totalCards}.");
            }
        }
    }

    private static void ValidateBettingRounds(IReadOnlyList<BettingRoundDto> rounds, List<string> errors)
    {
        if (rounds.Count == 0)
        {
            errors.Add("At least one betting round is required.");
            return;
        }

        var orders = rounds.Select(r => r.Order).ToList();
        if (orders.Count != orders.Distinct().Count())
        {
            errors.Add("Betting round orders must be unique.");
        }

        var expectedOrders = Enumerable.Range(0, rounds.Count).ToList();
        if (!orders.OrderBy(o => o).SequenceEqual(expectedOrders))
        {
            errors.Add("Betting round orders must be sequential starting from 0.");
        }

        foreach (var round in rounds)
        {
            if (string.IsNullOrWhiteSpace(round.Name))
            {
                errors.Add($"Betting round at order {round.Order} must have a name.");
            }

            if (round.CommunityCardsDealt < 0)
            {
                errors.Add($"CommunityCardsDealt cannot be negative for round '{round.Name}'.");
            }

            if (round.HoleCardsDealt < 0)
            {
                errors.Add($"HoleCardsDealt cannot be negative for round '{round.Name}'.");
            }

            if (round.MinBetMultiplier <= 0)
            {
                errors.Add($"MinBetMultiplier must be positive for round '{round.Name}'.");
            }

            if (round.MaxRaises is not null && round.MaxRaises < 0)
            {
                errors.Add($"MaxRaises cannot be negative for round '{round.Name}'.");
            }
        }
    }

    private static void ValidateHoleCardRules(HoleCardRulesDto holeRules, List<string> errors)
    {
        if (holeRules.Count < 1)
        {
            errors.Add("HoleCardRules.Count must be at least 1.");
        }

        if (holeRules.MinUsedInHand < 0)
        {
            errors.Add("HoleCardRules.MinUsedInHand cannot be negative.");
        }

        if (holeRules.MaxUsedInHand < holeRules.MinUsedInHand)
        {
            errors.Add("HoleCardRules.MaxUsedInHand cannot be less than MinUsedInHand.");
        }

        if (holeRules.MaxUsedInHand > holeRules.Count)
        {
            errors.Add("HoleCardRules.MaxUsedInHand cannot exceed the number of hole cards.");
        }

        if (holeRules.AllowDraw && holeRules.MaxDrawCount < 1)
        {
            errors.Add("When AllowDraw is true, MaxDrawCount must be at least 1.");
        }

        if (!holeRules.AllowDraw && holeRules.MaxDrawCount > 0)
        {
            errors.Add("MaxDrawCount should be 0 when AllowDraw is false.");
        }
    }

    private static void ValidateCommunityCardRules(CommunityCardRulesDto? communityRules, HoleCardRulesDto holeRules, List<string> errors)
    {
        if (communityRules is null)
        {
            return;
        }

        if (communityRules.TotalCount < 0)
        {
            errors.Add("CommunityCardRules.TotalCount cannot be negative.");
        }

        if (communityRules.MinUsedInHand < 0)
        {
            errors.Add("CommunityCardRules.MinUsedInHand cannot be negative.");
        }

        if (communityRules.MaxUsedInHand < communityRules.MinUsedInHand)
        {
            errors.Add("CommunityCardRules.MaxUsedInHand cannot be less than MinUsedInHand.");
        }

        if (communityRules.MaxUsedInHand > communityRules.TotalCount)
        {
            errors.Add("CommunityCardRules.MaxUsedInHand cannot exceed TotalCount.");
        }
    }

    private static void ValidateAnteBlindRules(AnteBlindRulesDto? anteBlindRules, List<string> errors)
    {
        if (anteBlindRules is null)
        {
            return;
        }

        if (anteBlindRules.HasAnte && anteBlindRules.AntePercentage <= 0)
        {
            errors.Add("AntePercentage must be positive when HasAnte is true.");
        }

        if (!anteBlindRules.HasAnte && anteBlindRules.AntePercentage > 0)
        {
            errors.Add("AntePercentage should be 0 when HasAnte is false.");
        }
    }

    private static void ValidateWildcardRules(WildcardRulesDto? wildcardRules, List<string> errors)
    {
        if (wildcardRules is null || !wildcardRules.Enabled)
        {
            return;
        }

        if (!wildcardRules.Dynamic && (wildcardRules.WildcardCards is null || wildcardRules.WildcardCards.Count == 0))
        {
            errors.Add("WildcardCards must be specified when wildcards are enabled and not dynamic.");
        }

        if (wildcardRules.Dynamic && string.IsNullOrWhiteSpace(wildcardRules.DynamicRule))
        {
            errors.Add("DynamicRule must be specified when Dynamic wildcards are enabled.");
        }
    }

    private static void ValidateHiLoRules(HiLoRulesDto? hiLoRules, List<string> errors)
    {
        if (hiLoRules is null || !hiLoRules.Enabled)
        {
            return;
        }

        if (hiLoRules.LowQualifier < 0 || hiLoRules.LowQualifier > 8)
        {
            errors.Add("LowQualifier must be between 0 (no qualifier) and 8 (eight-or-better).");
        }
    }

    private static void ValidateHandSizeConsistency(RuleSetDto ruleSet, List<string> errors)
    {
        const int StandardHandSize = 5;

        var maxHoleUsed = ruleSet.HoleCardRules.MaxUsedInHand;
        var maxCommunityUsed = ruleSet.CommunityCardRules?.MaxUsedInHand ?? 0;

        if (maxHoleUsed + maxCommunityUsed < StandardHandSize)
        {
            // This is valid for some games, but let's check min requirements
            var minHoleUsed = ruleSet.HoleCardRules.MinUsedInHand;
            var minCommunityUsed = ruleSet.CommunityCardRules?.MinUsedInHand ?? 0;

            if (minHoleUsed + minCommunityUsed > StandardHandSize)
            {
                errors.Add($"Minimum cards required ({minHoleUsed} hole + {minCommunityUsed} community) exceeds standard hand size of {StandardHandSize}.");
            }
        }

        // Validate total community cards dealt in rounds matches CommunityCardRules.TotalCount
        if (ruleSet.CommunityCardRules is not null)
        {
            var totalCommunityDealt = ruleSet.BettingRounds.Sum(r => r.CommunityCardsDealt);
            if (totalCommunityDealt != ruleSet.CommunityCardRules.TotalCount)
            {
                errors.Add($"Total community cards dealt across rounds ({totalCommunityDealt}) does not match CommunityCardRules.TotalCount ({ruleSet.CommunityCardRules.TotalCount}).");
            }
        }
    }
}

/// <summary>
/// Exception thrown when ruleset validation fails.
/// </summary>
public class RuleSetValidationException : Exception
{
    /// <summary>
    /// The validation errors that caused this exception.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates a new RuleSetValidationException with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public RuleSetValidationException(IReadOnlyList<string> errors)
        : base($"Ruleset validation failed: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }
}
