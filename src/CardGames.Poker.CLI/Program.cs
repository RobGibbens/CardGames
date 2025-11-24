using CardGames.Poker.CLI.Deal;
using CardGames.Poker.CLI.Simulation;
using Spectre.Console;
using Spectre.Console.Cli;

// If no arguments provided, show interactive menu
if (args.Length == 0)
{
    RunInteractiveMenu();
}
else
{
    var app = CreateCommandApp();
    app.Run(args);
}

static void RunInteractiveMenu()
{
    AnsiConsole.Write(
        new FigletText("Poker-CLI")
            .LeftJustified()
            .Color(Color.Green));
    AnsiConsole.Write(new Rule());
    
    var mode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]What would you like to do?[/]")
            .AddChoices("Deal Cards (Automated Dealer)", "Run Simulation (Manual Setup)", "Exit"));

    switch (mode)
    {
        case "Deal Cards (Automated Dealer)":
            RunDealMenu();
            break;
        case "Run Simulation (Manual Setup)":
            RunSimulationMenu();
            break;
        case "Exit":
            return;
    }
}

static void RunDealMenu()
{
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select game type:[/]")
            .AddChoices("Texas Holdem", "Omaha", "7-Card Stud", "Back"));

    var app = CreateCommandApp();
    
    switch (gameType)
    {
        case "Texas Holdem":
            app.Run(new[] { "deal", "holdem" });
            break;
        case "Omaha":
            app.Run(new[] { "deal", "omaha" });
            break;
        case "7-Card Stud":
            app.Run(new[] { "deal", "stud" });
            break;
        case "Back":
            RunInteractiveMenu();
            break;
    }
}

static void RunSimulationMenu()
{
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select simulation type:[/]")
            .AddChoices("Texas Holdem", "Omaha", "7-Card Stud", "Back"));

    var app = CreateCommandApp();
    
    switch (gameType)
    {
        case "Texas Holdem":
            app.Run(new[] { "sim", "holdem" });
            break;
        case "Omaha":
            app.Run(new[] { "sim", "omaha" });
            break;
        case "7-Card Stud":
            app.Run(new[] { "sim", "stud" });
            break;
        case "Back":
            RunInteractiveMenu();
            break;
    }
}

static CommandApp CreateCommandApp()
{
    var app = new CommandApp();
    app.Configure(configuration => 
    {
        configuration.SetApplicationName("poker-cli");
        
        configuration.AddBranch<SimulationSettings>("sim", sim =>
        {
            sim
                .AddCommand<StudSimulationCommand>("stud-hi")
                .WithAlias("7cs-hi")
                .WithAlias("stud")
                .WithDescription("Runs a 7-card Stud hi simulation.");

            sim
                .AddCommand<HoldemSimulationCommand>("holdem")
                .WithAlias("nlh")
                .WithAlias("lhe")
                .WithDescription("Runs a Holdem simulation.");

            sim
                .AddCommand<OmahaSimulationCommand>("omaha")
                .WithAlias("plo")
                .WithDescription("Runs an Omaha simulation.");
        });
        
        configuration.AddBranch<DealSettings>("deal", deal =>
        {
            deal
                .AddCommand<HoldemDealCommand>("holdem")
                .WithAlias("nlh")
                .WithAlias("lhe")
                .WithDescription("Deal a Texas Holdem hand with automated dealer.");

            deal
                .AddCommand<OmahaDealCommand>("omaha")
                .WithAlias("plo")
                .WithDescription("Deal an Omaha hand with automated dealer.");

            deal
                .AddCommand<StudDealCommand>("stud")
                .WithAlias("7cs")
                .WithAlias("stud-hi")
                .WithDescription("Deal a 7-card Stud hand with automated dealer.");
        });
    });
    
    return app;
}
