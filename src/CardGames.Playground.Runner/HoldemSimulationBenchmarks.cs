using BenchmarkDotNet.Attributes;
using CardGames.Poker.Simulations.Holdem;

namespace CardGames.Poker.Benchmarks;

[MemoryDiagnoser]
public class HoldemSimulationBenchmarks
{
	[Params(100, 1000)] public int NumberOfHands;

	private HoldemSimulation _fiveHandedSimulation;

	private HoldemSimulation _headsUpSimulation;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_headsUpSimulation = HoldemSimulations.HeadsUp();
		_fiveHandedSimulation = HoldemSimulations.FiveHanded();
	}

	[Benchmark]
	public HoldemSimulationResult Holdem_HeadsUp()
	{
		return _headsUpSimulation.SimulateWithFullDeck(NumberOfHands);
	}

	[Benchmark]
	public HoldemSimulationResult Holdem_FiveHanded()
	{
		return _fiveHandedSimulation.SimulateWithFullDeck(NumberOfHands);
	}
}