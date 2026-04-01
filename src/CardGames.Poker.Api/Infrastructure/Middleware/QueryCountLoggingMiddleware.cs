using System.Diagnostics;
using CardGames.Poker.Api.Infrastructure.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware that logs the total SQL command count per HTTP request.
/// Place after ExceptionHandlingMiddleware so it wraps the real request work.
/// </summary>
public sealed class QueryCountLoggingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<QueryCountLoggingMiddleware> _logger;

	public QueryCountLoggingMiddleware(RequestDelegate next, ILogger<QueryCountLoggingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var queryCountContext = context.RequestServices.GetService<QueryCountContext>();
		queryCountContext?.Reset();

		try
		{
			await _next(context);
		}
		finally
		{
			if (queryCountContext is not null)
			{
				var count = queryCountContext.CommandCount;
				var path = context.Request.Path;
				var method = context.Request.Method;
				var statusCode = context.Response.StatusCode;

				if (count > 0)
				{
					_logger.LogInformation(
						"SQL: {SqlCommandCount} queries | {HttpMethod} {RequestPath} → {StatusCode}",
						count, method, path, statusCode);
				}

				Activity.Current?.SetTag("sql.command_count", count);
			}
		}
	}
}
