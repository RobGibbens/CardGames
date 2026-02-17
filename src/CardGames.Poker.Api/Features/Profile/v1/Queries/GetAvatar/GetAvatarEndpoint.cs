using System.Text;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetAvatar;

public static class GetAvatarEndpoint
{
	private const string DefaultAvatarSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"96\" height=\"96\" viewBox=\"0 0 96 96\"><rect width=\"96\" height=\"96\" fill=\"#1f2937\"/><circle cx=\"48\" cy=\"36\" r=\"18\" fill=\"#9ca3af\"/><path d=\"M18 84c2-16 14-26 30-26s28 10 30 26\" fill=\"#9ca3af\"/></svg>";
	private static readonly byte[] DefaultAvatarBytes = Encoding.UTF8.GetBytes(DefaultAvatarSvg);

	public static RouteGroupBuilder MapGetAvatar(this RouteGroupBuilder group)
	{
		group.MapGet("avatar/{userId}",
				async (string userId, CardsDbContext dbContext, IAvatarStorageService avatarStorageService, CancellationToken cancellationToken) =>
				{
					if (string.IsNullOrWhiteSpace(userId))
					{
						return Results.BadRequest(new { message = "User id is required." });
					}

					var avatarReference = await dbContext.Users
						.AsNoTracking()
						.Where(u => u.Id == userId)
						.Select(u => u.AvatarUrl)
						.FirstOrDefaultAsync(cancellationToken);

					if (!string.IsNullOrWhiteSpace(avatarReference))
					{
						var storedAvatar = await avatarStorageService.TryGetAsync(avatarReference, cancellationToken);
						if (storedAvatar is not null)
						{
							return Results.File(storedAvatar.Content, storedAvatar.ContentType);
						}

						if (Uri.TryCreate(avatarReference, UriKind.Absolute, out var absoluteAvatarUri)
							&& (string.Equals(absoluteAvatarUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
								|| string.Equals(absoluteAvatarUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
						{
							return Results.Redirect(absoluteAvatarUri.ToString(), permanent: false, preserveMethod: false);
						}
					}

					return Results.File(DefaultAvatarBytes, "image/svg+xml");
				})
			.WithName("GetAvatar")
			.WithSummary("Get avatar")
			.WithDescription("Returns the avatar image for the specified user id.")
			.Produces(StatusCodes.Status200OK, contentType: "image/svg+xml")
			.Produces(StatusCodes.Status200OK, contentType: "image/png")
			.Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
			.Produces(StatusCodes.Status200OK, contentType: "image/gif")
			.Produces(StatusCodes.Status200OK, contentType: "image/webp")
			.Produces(StatusCodes.Status302Found)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}