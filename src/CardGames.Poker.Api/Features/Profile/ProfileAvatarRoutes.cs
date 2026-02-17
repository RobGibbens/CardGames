namespace CardGames.Poker.Api.Features.Profile;

public static class ProfileAvatarRoutes
{
	public static string BuildAvatarPath(string userId)
	{
		return $"/api/v1/profile/avatar/{Uri.EscapeDataString(userId)}";
	}
}