using System.Threading;
using CardGames.Core.Extensions;
using CardGames.Poker.CLI.Evaluation;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Evaluation;
using CardGames.Playground.Simulations.Stud;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Simulation;

internal class KingsAndLowsSimulationCommand : Command<SimulationSettings>
{
    private static readonly SpectreLogger Logger = new();

    private static KingsAndLowsSimulation CreateSimulation()
    {
        var simulation = new KingsAndLowsSimulation();
        do
        {
            simulation.WithPlayer(GetPlayer());
        }
        while (AnsiConsole.Confirm("Do you want to add another player?"));

        Logger.Paragraph("Other configuration");
        var deadCards = Prompt.PromptForRangeOfCards("Dead cards:", 0, 52);
        simulation.WithDeadCards(deadCards);

        var kingRequired = AnsiConsole.Confirm("Require King to use low card as wild?", false);
        if (kingRequired)
        {
            simulation.WithKingRequired();
        }

        return simulation;
    }

    private static KingsAndLowsPlayer GetPlayer()
    {
        Logger.Paragraph("Add Player");

        var name = AnsiConsole.Ask<string>("Player Name: ");
        var holeCards = Prompt.PromptForRangeOfCards("Hole Cards: ", 0, 3);
        var boardCards = Prompt.PromptForRangeOfCards("Board Cards: ", 0, 4);

        return new KingsAndLowsPlayer(name)
            .WithHoleCards(holeCards)
            .WithBoardCards(boardCards);
    }

    private static void PrintResults(KingsAndLowsSimulationResult result)
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
