namespace CardGames.Core.Random;

public class StandardRandomNumberGenerator : IRandomNumberGenerator
{
	private readonly System.Random _random = new();

	public int Next(int upperBound)
	{
		return _random.Next(upperBound);
	}
}