using System.Diagnostics;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using MediatR;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

public sealed class TracingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Use the short type name as a stable, low-cardinality span name. Request property
        // values are intentionally NOT recorded on the span: dumping them would leak secrets,
        // tokens, and PII (passwords, emails, private cards, full payloads).
        var requestType = typeof(TRequest).Name;
        using var activity = PokerActivitySource.Source.StartActivity(
            $"mediatr.{requestType}",
            ActivityKind.Internal);

        activity?.SetTag("mediatr.request_type", requestType);

        try
        {
            var response = await next(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
