using CardGames.Poker.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UploadAvatar;

public static class UploadAvatarEndpoint
{
	private static readonly HashSet<string> AllowedContentTypes =
	[
		"image/jpeg",
		"image/png",
		"image/gif",
		"image/webp"
	];

	public static RouteGroupBuilder MapUploadAvatar(this RouteGroupBuilder group)
	{
		group.MapPost("avatar",
				async (IFormFile? file, IAvatarStorageService avatarStorageService, IOptions<AvatarStorageOptions> optionsProvider, CancellationToken cancellationToken) =>
				{
					var options = optionsProvider.Value;

					if (file is null || file.Length <= 0)
					{
						return Results.BadRequest(new { message = "Avatar file is required." });
					}

					if (file.Length > options.MaxUploadBytes)
					{
						return Results.BadRequest(new { message = $"Avatar file exceeds the maximum allowed size of {options.MaxUploadBytes} bytes." });
					}

					if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
					{
						return Results.BadRequest(new { message = "Unsupported avatar file type. Allowed types: image/jpeg, image/png, image/gif, image/webp." });
					}

					await using var stream = file.OpenReadStream();
					try
					{
						var avatarUrl = await avatarStorageService.UploadAsync(stream, file.FileName, file.ContentType, cancellationToken);
						avatarUrl = ResolvePublicAvatarUrl(avatarUrl, options.PublicBaseUrl);
						return Results.Ok(new UploadAvatarResponse(avatarUrl));
					}
					catch (InvalidOperationException ex)
					{
						return Results.Problem(title: "Avatar upload configuration error", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
					}
					catch (Exception ex)
					{
						return Results.Problem(title: "Avatar upload failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
					}
				})
			.WithName("UploadAvatar")
			.WithSummary("Upload avatar")
			.WithDescription("Upload an avatar image to blob storage and return the resulting URL.")
			.DisableAntiforgery()
			.Accepts<IFormFile>("multipart/form-data")
			.Produces<UploadAvatarResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous();

		return group;
	}

	private static string ResolvePublicAvatarUrl(string avatarUrl, string? publicBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(publicBaseUrl))
		{
			return avatarUrl;
		}

		if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var avatarUri))
		{
			return avatarUrl;
		}

		if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
		{
			return avatarUrl;
		}

		if (!string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			return avatarUrl;
		}

		var normalizedBase = publicBaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
			? publicBaseUri
			: new Uri(publicBaseUri.AbsoluteUri + "/", UriKind.Absolute);

		var relativeAvatarPath = avatarUri.PathAndQuery.TrimStart('/');
		if (string.IsNullOrWhiteSpace(relativeAvatarPath))
		{
			return avatarUrl;
		}

		return new Uri(normalizedBase, relativeAvatarPath).ToString();
	}
}

public sealed record UploadAvatarResponse(string AvatarUrl);
