using CardGames.Poker.Api.Common.Mapping;

namespace CardGames.Poker.Api.Features.Variants;

/// <summary>
/// Module for variant-related endpoints using vertical slice architecture.
/// </summary>
public static class VariantsModule
{
    /// <summary>
    /// Maps the variant endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapVariantsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetVariantsEndpoint();
        app.MapGetVariantByIdEndpoint();
        return app;
    }
}

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
