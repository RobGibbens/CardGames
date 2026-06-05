using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Simulations.Holdem;

namespace CardGames.Poker.Benchmarks;

internal static class HoldemSimulations
{
	public static HoldemSimulation HeadsUp()
	{
		return new HoldemSimulation()
			.WithPlayer("James", "8s 6d".ToCards())
			.WithPlayer("Jimmy", "Ad Kd".ToCards())
			.WithFlop("3h 6c Qd".ToCards());
	}

	public static HoldemSimulation FiveHanded()
	{
		return new HoldemSimulation()
			.WithPlayer("Anton", "8s 6s".ToCards())
			.WithPlayer("Bernd", "Ad Kd".ToCards())
			.WithPlayer("Cathy", "2d 2c".ToCards())
			.WithPlayer("Derek", "Qh Jh".ToCards())
			.WithPlayer("Ernst", "Ts Th".ToCards());
	}
}