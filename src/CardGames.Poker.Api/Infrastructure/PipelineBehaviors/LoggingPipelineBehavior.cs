using MediatR;
using System.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

public class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
private readonly ILogger<LoggingPipelineBehavior<TRequest, TResponse>> _logger;

public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
{
_logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
{
// Use the short type name as a stable, low-cardinality correlation key. The request
// instance itself is intentionally NOT logged: dumping every property would leak
// secrets, tokens, and PII (passwords, emails, private cards, full payloads) and add
// high-volume noise. Structured identifiers should be logged explicitly by handlers.
var requestType = typeof(TRequest).Name;

using var scope = _logger.BeginScope(new Dictionary<string, object>
{
["RequestType"] = requestType
});

var stopwatch = Stopwatch.StartNew();

try
{
_logger.LogDebug("Handling {RequestType}", requestType);

var response = await next(cancellationToken);

stopwatch.Stop();
_logger.LogInformation(
"Handled {RequestType} in {ElapsedMilliseconds}ms",
requestType,
stopwatch.ElapsedMilliseconds);

return response;
}
catch (Exception ex)
{
stopwatch.Stop();
_logger.LogError(
ex,
"Error handling {RequestType} after {ElapsedMilliseconds}ms",
requestType,
stopwatch.ElapsedMilliseconds);
throw;
}
}
}
