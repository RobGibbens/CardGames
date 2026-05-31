using System.Diagnostics;
using System.Reflection;
using MediatR;

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

var scopeValues = new Dictionary<string, object>
{
["RequestType"] = requestType
};
AddScopeValue(scopeValues, request, "GameId");
AddScopeValue(scopeValues, request, "HandNumber");
AddScopeValue(scopeValues, request, "UserId");
AddScopeValue(scopeValues, request, "LeagueId");
AddScopeValue(scopeValues, request, "ConnectionId");

using var scope = _logger.BeginScope(scopeValues);

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

private static void AddScopeValue(Dictionary<string, object> scopeValues, TRequest request, string propertyName)
{
var property = typeof(TRequest).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
var value = property?.GetValue(request);
if (value is not null)
{
scopeValues[propertyName] = value;
}
}
}
