using CardGames.Poker.Api.Common.Mapping;
using CardGames.Poker.Variants;
using MediatR;

namespace CardGames.Poker.Api.Features.Variants;

/// <summary>
/// Query to get a variant by ID.
/// </summary>
public record GetVariantByIdQuery(string VariantId) : IRequest<IResult>;

/// <summary>
/// Handler for GetVariantByIdQuery.
/// </summary>
public sealed class GetVariantByIdHandler : IRequestHandler<GetVariantByIdQuery, IResult>
{
    private readonly IGameVariantProvider _variantProvider;

    public GetVariantByIdHandler(IGameVariantProvider variantProvider)
    {
        _variantProvider = variantProvider;
    }

    public Task<IResult> Handle(GetVariantByIdQuery request, CancellationToken cancellationToken)
    {
        var variant = _variantProvider.GetVariant(request.VariantId);

        if (variant == null)
        {
            var notFoundResponse = new VariantResponse(
                Success: false,
                Error: $"Variant '{request.VariantId}' not found.");
            return Task.FromResult(Results.NotFound(notFoundResponse));
        }

        var response = new VariantResponse(
            Success: true,
            Variant: ApiMapper.ToVariantDto(variant));

        return Task.FromResult(Results.Ok(response));
    }
}

/// <summary>
/// Endpoint for getting a variant by ID.
/// </summary>
public static class GetVariantByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetVariantByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/variants/{variantId}", async (
            string variantId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            return await mediator.Send(new GetVariantByIdQuery(variantId), cancellationToken);
        })
        .WithName("GetVariantById")
        .WithTags("Variants");

        return app;
    }
}
