using System.Net;
using System.Text.Json;

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
		catch (Exception ex)
		{
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