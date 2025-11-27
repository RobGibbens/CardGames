using System;
using CardGames.Poker.CLI.Deal;
using CardGames.Poker.CLI.Play;
using CardGames.Poker.CLI.Simulation;
using Spectre.Console;
using Spectre.Console.Cli;

// Ensure UTF-8 encoding is set for proper Unicode character display
Console.OutputEncoding = System.Text.Encoding.UTF8;

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
    while (true)
    {
        AnsiConsole.Write(
            new FigletText("Poker-CLI")
                .LeftJustified()
                .Color(Color.Green));
        AnsiConsole.Write(new Rule());
        
        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to do?[/]")
                .AddChoices("Play Poker (With Betting)", "Deal Cards (Automated Dealer)", "Run Simulation (Manual Setup)", "Exit"));

        switch (mode)
        {
            case "Play Poker (With Betting)":
                RunPlayMenu();
                break;
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
}

static void RunPlayMenu()
{
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select game type:[/]")
            .AddChoices("5-Card Draw", "7-Card Stud", "Texas Hold 'Em", "Omaha", "Baseball", "Kings and Lows", "Follow the Queen", "Back"));

    var app = CreateCommandApp();
    
    switch (gameType)
    {
        case "5-Card Draw":
            app.Run(new[] { "play", "draw" });
            break;
        case "7-Card Stud":
            app.Run(new[] { "play", "stud" });
            break;
        case "Texas Hold 'Em":
            app.Run(new[] { "play", "holdem" });
            break;
        case "Omaha":
	        app.Run(new[] { "play", "omaha" });
	        break;
        case "Baseball":
            app.Run(new[] { "play", "baseball" });
            break;
        case "Kings and Lows":
            app.Run(new[] { "play", "kings-and-lows" });
            break;
        case "Follow the Queen":
            app.Run(new[] { "play", "follow-the-queen" });
            break;
        case "Back":
            break;
    }
}

static void RunDealMenu()
{
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select game type:[/]")
            .AddChoices("Texas Holdem", "Omaha", "7-Card Stud", "5-Card Draw", "Baseball", "Kings and Lows", "Follow the Queen", "Back"));

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
        case "5-Card Draw":
            app.Run(new[] { "deal", "draw" });
            break;
        case "Baseball":
            app.Run(new[] { "deal", "baseball" });
            break;
        case "Kings and Lows":
            app.Run(new[] { "deal", "kings-and-lows" });
            break;
        case "Follow the Queen":
            app.Run(new[] { "deal", "follow-the-queen" });
            break;
        case "Back":
            break;
    }
}

static void RunSimulationMenu()
{
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select simulation type:[/]")
            .AddChoices("Texas Holdem", "Omaha", "7-Card Stud", "5-Card Draw", "Baseball", "Kings and Lows", "Follow the Queen", "Back"));

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
        case "5-Card Draw":
            app.Run(new[] { "sim", "draw" });
            break;
        case "Baseball":
            app.Run(new[] { "sim", "baseball" });
            break;
        case "Kings and Lows":
            app.Run(new[] { "sim", "kings-and-lows" });
            break;
        case "Follow the Queen":
            app.Run(new[] { "sim", "follow-the-queen" });
            break;
        case "Back":
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

            sim
                .AddCommand<FiveCardDrawSimulationCommand>("draw")
                .WithAlias("5cd")
                .WithAlias("5-card-draw")
                .WithDescription("Runs a 5-card Draw simulation.");

            sim
                .AddCommand<BaseballSimulationCommand>("baseball")
                .WithDescription("Runs a Baseball simulation (3s and 9s wild, 4s grant extra cards).");

            sim
                .AddCommand<KingsAndLowsSimulationCommand>("kings-and-lows")
                .WithAlias("kal")
                .WithDescription("Runs a Kings and Lows simulation (lowest card is wild).");

            sim
                .AddCommand<FollowTheQueenSimulationCommand>("follow-the-queen")
                .WithAlias("ftq")
                .WithDescription("Runs a Follow the Queen simulation (Queens and following card are wild).");
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

            deal
                .AddCommand<FiveCardDrawDealCommand>("draw")
                .WithAlias("5cd")
                .WithAlias("5-card-draw")
                .WithDescription("Deal a 5-card Draw hand with automated dealer.");

            deal
                .AddCommand<BaseballDealCommand>("baseball")
                .WithDescription("Deal a Baseball hand (3s and 9s wild, 4s grant extra cards).");

            deal
                .AddCommand<KingsAndLowsDealCommand>("kings-and-lows")
                .WithAlias("kal")
                .WithDescription("Deal a Kings and Lows hand (lowest card is wild).");

            deal
                .AddCommand<FollowTheQueenDealCommand>("follow-the-queen")
                .WithAlias("ftq")
                .WithDescription("Deal a Follow the Queen hand (Queens and following card are wild).");
        });

        configuration.AddBranch<PlaySettings>("play", play =>
        {
            play
                .AddCommand<FiveCardDrawPlayCommand>("draw")
                .WithAlias("5cd")
                .WithAlias("5-card-draw")
                .WithDescription("Play 5-card Draw with betting.");

            play
                .AddCommand<SevenCardStudPlayCommand>("stud")
                .WithAlias("7cs")
                .WithAlias("7-card-stud")
                .WithDescription("Play 7-card Stud with betting.");

            play
                .AddCommand<HoldEmPlayCommand>("holdem")
                .WithAlias("nlh")
                .WithAlias("texas-holdem")
                .WithDescription("Play Texas Hold 'Em with betting (blinds structure).");

            play
                .AddCommand<OmahaPlayCommand>("omaha")
                .WithAlias("plo")
                .WithDescription("Play Omaha with betting (4 hole cards, must use exactly 2).");

            play
                .AddCommand<BaseballPlayCommand>("baseball")
                .WithDescription("Play Baseball with betting (3s and 9s wild, 4s grant extra cards).");

            play
                .AddCommand<KingsAndLowsPlayCommand>("kings-and-lows")
                .WithAlias("kal")
                .WithDescription("Play Kings and Lows with drop-or-stay and pot matching.");

            play
                .AddCommand<FollowTheQueenPlayCommand>("follow-the-queen")
                .WithAlias("ftq")
                .WithDescription("Play Follow the Queen with betting (Queens and following card are wild).");
        });
    });
    
    return app;
}
