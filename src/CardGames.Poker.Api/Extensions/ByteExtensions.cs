namespace CardGames.Poker.Api.Extensions;

public static class ByteExtensions
{
	extension(byte[] bytes)
	{
		public string ToBase64String()
		{
			return Convert.ToBase64String(bytes);
		}
	}
}