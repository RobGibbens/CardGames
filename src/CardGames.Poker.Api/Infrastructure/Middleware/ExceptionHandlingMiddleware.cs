using System.Net;
using System.Text.Json;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Infrastructure.Middleware;

public sealed class ExceptionHandlingMiddleware
{
	private readonly RequestDelegate _next;

	public ExceptionHandlingMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (GameAuthorizationException authEx)
		{
			if (context.Response.HasStarted)
				throw;

			context.Response.StatusCode = authEx.StatusCode;
			context.Response.ContentType = "application/json";
			await context.Response.WriteAsJsonAsync(new { Message = authEx.Message, Code = authEx.Code },
				new JsonSerializerOptions
				{
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				});
		}
		catch (Exception ex)
		{
			if (context.Response.HasStarted)
				throw;

			context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
			context.Response.ContentType = "application/json";

			var message = "An unexpected error occurred.";
#if DEBUG
			message = ex.Message;
#endif
			await context.Response.WriteAsJsonAsync(new { Message = message }, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});
		}
	}
}