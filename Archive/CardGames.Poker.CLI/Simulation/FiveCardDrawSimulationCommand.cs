using System.Threading;
using CardGames.Core.Extensions;
using CardGames.Poker.CLI.Evaluation;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Simulations.Draw;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Simulation;

internal class FiveCardDrawSimulationCommand : Command<SimulationSettings>
{
    private static readonly SpectreLogger Logger = new();

    private static FiveCardDrawSimulation CreateSimulation()
    {
        var simulation = new FiveCardDrawSimulation();
        do
        {
            simulation.WithPlayer(GetPlayer());
        }
        while (AnsiConsole.Confirm("Do you want to add another player?"));

        Logger.Paragraph("Other configuration");
        var deadCards = Prompt.PromptForRangeOfCards("Dead cards:", 0, 52);
        simulation.WithDeadCards(deadCards);

        return simulation;
    }

    private static FiveCardDrawPlayer GetPlayer()
    {
        Logger.Paragraph("Add Player");

        var name = AnsiConsole.Ask<string>("Player Name: ");
        var cards = Prompt.PromptForRangeOfCards("Cards: ", 0, 5);

        return new FiveCardDrawPlayer(name).WithCards(cards);
    }

    private static void PrintResults(FiveCardDrawSimulationResult result)
        => new[]
            {
                HandsEvaluation
                    .GroupByWins(result.Hands)
                    .ToArtefact(),
                HandsEvaluation
                    .AllMadeHandDistributions(result.Hands)
                    .ToArtefact()
            }
            .ForEach(Logger.LogArtefact);

    protected override int Execute(CommandContext context, SimulationSettings settings, CancellationToken cancellationToken)
    {
        Logger.LogApplicationStart();

        var simulation = CreateSimulation();
        var numberOfHands = settings.NumberOfHands == default
            ? AnsiConsole.Ask<int>("How many hands?")
            : settings.NumberOfHands;

        var result = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Arrow3)
            .Start("Simulating ... ", ctx => simulation.Simulate(numberOfHands));

        AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Arrow3)
            .Start("Evaluating ... ", ctx => PrintResults(result));

        return 0;
    }
}
