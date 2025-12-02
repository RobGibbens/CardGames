using CardGames.Poker.Api.Common.Mapping;
using CardGames.Poker.Variants;
using MediatR;

namespace CardGames.Poker.Api.Features.Variants;

/// <summary>
/// Query to get all variants.
/// </summary>
public record GetVariantsQuery : IRequest<IResult>;

/// <summary>
/// Handler for GetVariantsQuery.
/// </summary>
public sealed class GetVariantsHandler : IRequestHandler<GetVariantsQuery, IResult>
{
    private readonly IGameVariantProvider _variantProvider;

    public GetVariantsHandler(IGameVariantProvider variantProvider)
    {
        _variantProvider = variantProvider;
    }

    public Task<IResult> Handle(GetVariantsQuery request, CancellationToken cancellationToken)
    {
        var variants = _variantProvider.GetAllVariants();
        var response = new VariantsListResponse(
            Success: true,
            Variants: ApiMapper.ToVariantDtos(variants));

        return Task.FromResult(Results.Ok(response));
    }
}

/// <summary>
/// Endpoint for getting variants.
/// </summary>
public static class GetVariantsEndpoint
{
    public static IEndpointRouteBuilder MapGetVariantsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/variants", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            return await mediator.Send(new GetVariantsQuery(), cancellationToken);
        })
        .WithName("GetVariants")
        .WithTags("Variants");

        return app;
    }
}
