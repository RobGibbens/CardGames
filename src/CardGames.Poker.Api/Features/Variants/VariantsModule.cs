using CardGames.Poker.Variants;

namespace CardGames.Poker.Api.Features.Variants;

/// <summary>
/// API endpoints for game variant discovery and management.
/// </summary>
public static class VariantsModule
{
    /// <summary>
    /// Maps the variant endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapVariantsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/variants")
            .WithTags("Variants");

        group.MapGet("", GetVariantsAsync)
            .WithName("GetVariants");

        group.MapGet("{variantId}", GetVariantByIdAsync)
            .WithName("GetVariantById");

        return app;
    }

    private static IResult GetVariantsAsync(IGameVariantProvider variantProvider)
    {
        var variants = variantProvider.GetAllVariants();
        var response = new VariantsListResponse(
            Success: true,
            Variants: variants.Select(v => new VariantDto(
                v.Id,
                v.Name,
                v.Description,
                v.MinPlayers,
                v.MaxPlayers)).ToList());

        return Results.Ok(response);
    }

    private static IResult GetVariantByIdAsync(
        string variantId,
        IGameVariantProvider variantProvider)
    {
        var variant = variantProvider.GetVariant(variantId);

        if (variant == null)
        {
            return Results.NotFound(new VariantResponse(
                Success: false,
                Error: $"Variant '{variantId}' not found."));
        }

        return Results.Ok(new VariantResponse(
            Success: true,
            Variant: new VariantDto(
                variant.Id,
                variant.Name,
                variant.Description,
                variant.MinPlayers,
                variant.MaxPlayers)));
    }
}

/// <summary>
/// Data transfer object for variant information.
/// </summary>
public record VariantDto(
    string Id,
    string Name,
    string? Description,
    int MinPlayers,
    int MaxPlayers);

/// <summary>
/// Response containing a list of variants.
/// </summary>
public record VariantsListResponse(
    bool Success,
    IReadOnlyList<VariantDto>? Variants = null,
    string? Error = null);

/// <summary>
/// Response containing a single variant.
/// </summary>
public record VariantResponse(
    bool Success,
    VariantDto? Variant = null,
    string? Error = null);
