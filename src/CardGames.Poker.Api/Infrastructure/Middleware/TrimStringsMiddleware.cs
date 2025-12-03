using System.Text;
using System.Text.Json;

namespace CardGames.Poker.Api.Infrastructure.Middleware;

public class TrimStringsMiddleware
{
	private readonly RequestDelegate _next;

	public TrimStringsMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
		{
			context.Request.EnableBuffering(); // Allows us to read the request body multiple times

			using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
			var body = await reader.ReadToEndAsync();
			context.Request.Body.Position = 0; // Reset the stream position for the next reader

			if (!string.IsNullOrEmpty(body))
			{
				var trimmedBody = TrimStringPropertiesInJson(body);
				var bytes = Encoding.UTF8.GetBytes(trimmedBody);

				context.Request.Body = new MemoryStream(bytes); // Replace the request body with trimmed JSON
			}
		}

		await _next(context);
	}

	private string TrimStringPropertiesInJson(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		var trimmedDictionary = TrimJsonElement(root);
		return JsonSerializer.Serialize(trimmedDictionary);
	}
	private Dictionary<string, object?> TrimJsonElement(JsonElement element)
	{
		var result = new Dictionary<string, object?>();

		foreach (var property in element.EnumerateObject())
		{
			if (property.Value.ValueKind == JsonValueKind.String)
			{
				result[property.Name] = property.Value.GetString()?.Trim();
			}
			else if (property.Value.ValueKind == JsonValueKind.Object)
			{
				result[property.Name] = TrimJsonElement(property.Value);
			}
			else if (property.Value.ValueKind == JsonValueKind.Array)
			{
				var list = new List<object?>();
				foreach (var arrayElement in property.Value.EnumerateArray())
				{
					if (arrayElement.ValueKind == JsonValueKind.String)
					{
						list.Add(arrayElement.GetString()?.Trim());
					}
					else if (arrayElement.ValueKind == JsonValueKind.Object)
					{
						list.Add(TrimJsonElement(arrayElement));
					}
					else
					{
						list.Add(arrayElement.ToString());
					}
				}
				result[property.Name] = list;
			}
			else
			{
				result[property.Name] = property.Value;
			}
		}

		return result;
	}
}