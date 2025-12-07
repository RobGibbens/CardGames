namespace CardGames.Poker.Api.Extensions;

public static class StringExtensions
{
	extension(string input)
	{
		public string TrimPrefix(string prefix)
		{
			ArgumentNullException.ThrowIfNull(input);

			ArgumentNullException.ThrowIfNull(prefix);

			return input.StartsWith(prefix, StringComparison.Ordinal)
				? input[prefix.Length..]
				: input;
		}

		public byte[] FromBase64String()
		{
			return Convert.FromBase64String(input);
		}
	}
}