namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Tollbooth orbit.
///     Ante 5, bring-in 10, small bet 10, big bet 20.
///     The big blind's deuce of clubs is the bring-in in every hand.
///     Furthest is free, nearest costs one ante, and the deck costs two antes.
///     Display cards are visible and are not part of the seven-card hand.
/// </summary>
public static class TollboothFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = 35;
	public const int CalledPot = 75;

	public static TollboothHandScript FoldToBringIn { get; } = new()
	{
		Name = "Fold to the bring-in",
		Holdings = Hands(
			Holes("5d 6d", "Kh"),
			Holes("5h 6h", "9h"),
			Holes("5c 6c", "2c"),
			Holes("5s 6s", "Jh"),
			Holes("7d 8d", "Th")),
		Steps =
		[
			TollboothStep.Fold(HoldEmPosition.UnderTheGun, TollboothStreet.Third, Doors("Kh", "9h", "2c", "Jh", "Th")),
			TollboothStep.Fold(HoldEmPosition.Cutoff, TollboothStreet.Third),
			TollboothStep.Fold(HoldEmPosition.Button, TollboothStreet.Third),
			TollboothStep.Fold(HoldEmPosition.SmallBlind, TollboothStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		InitialNearest = "3c",
		InitialFurthest = "4c",
		ShowdownNearest = "3c",
		ShowdownFurthest = "4c",
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     Every player calls the bring-in, then checks. Every later card is the free furthest card,
	///     so the pot stays at the called amount. The big blind's pair of deuces acts first after third street.
	/// </summary>
	public static TollboothHandScript FreeCheckdown { get; } = new()
	{
		Name = "Free checkdown",
		Holdings = Hands(
			Holes("8d 9d", "Kh", fourth: "6s", fifth: "Ts", sixth: "Qd", seventh: "9c"),
			Holes("5h 6h", "9h", fourth: "4s", fifth: "8s", sixth: "Js", seventh: "2d"),
			Holes("Ah Ad", "2c", fourth: "2h", fifth: "3h", sixth: "4h", seventh: "Tc"),
			Holes("4d 5d", "Jh", fourth: "7s", fifth: "Qs", sixth: "8c", seventh: "Jc"),
			Holes("6d 7d", "Th", fourth: "5s", fifth: "9s", sixth: "Qc", seventh: "3d")),
		Steps =
		[
			..BringInCalls(Doors("Kh", "9h", "2c", "Jh", "Th"),
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..Furthest(TollboothStreet.Fourth, "3c", CalledPot, "8s",
				(HoldEmPosition.SmallBlind, "4s"),
				(HoldEmPosition.Cutoff, "5s"),
				(HoldEmPosition.Button, "6s"),
				(HoldEmPosition.BigBlind, "2h"),
				(HoldEmPosition.UnderTheGun, "7s")),
			..Checks(TollboothStreet.Fourth, Boards("Kh 6s", "9h 4s", "2c 2h", "Jh 7s", "Th 5s"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..Furthest(TollboothStreet.Fifth, "3c", CalledPot, "Js",
				(HoldEmPosition.SmallBlind, "8s"),
				(HoldEmPosition.Cutoff, "9s"),
				(HoldEmPosition.Button, "Ts"),
				(HoldEmPosition.BigBlind, "3h"),
				(HoldEmPosition.UnderTheGun, "Qs")),
			..Checks(TollboothStreet.Fifth, Boards("Kh 6s Ts", "9h 4s 8s", "2c 2h 3h", "Jh 7s Qs", "Th 5s 9s"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..Furthest(TollboothStreet.Sixth, "3c", CalledPot, "2d",
				(HoldEmPosition.SmallBlind, "Js"),
				(HoldEmPosition.Cutoff, "Qc"),
				(HoldEmPosition.Button, "Qd"),
				(HoldEmPosition.BigBlind, "4h"),
				(HoldEmPosition.UnderTheGun, "8c")),
			..Checks(TollboothStreet.Sixth, Boards(
					"Kh 6s Ts Qd", "9h 4s 8s Js", "2c 2h 3h 4h", "Jh 7s Qs 8c", "Th 5s 9s Qc"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..Furthest(TollboothStreet.Seventh, "3c", CalledPot, "Kc",
				(HoldEmPosition.SmallBlind, "2d"),
				(HoldEmPosition.Cutoff, "3d"),
				(HoldEmPosition.Button, "9c"),
				(HoldEmPosition.BigBlind, "Tc"),
				(HoldEmPosition.UnderTheGun, "Jc")),
			..Checks(TollboothStreet.Seventh, Boards(
					"Kh 6s Ts Qd", "9h 4s 8s Js", "2c 2h 3h 4h", "Jh 7s Qs 8c", "Th 5s 9s Qc"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 60,
			[HoldEmPosition.Button] = -15,
			[HoldEmPosition.SmallBlind] = -15,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Cutoff] = -15
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = CalledPot,
		InitialNearest = "3c",
		InitialFurthest = "4s",
		ShowdownNearest = "3c",
		ShowdownFurthest = "Kc",
		OpeningPot = OpeningPot,
		WonByFold = false
	};

	/// <summary>
	///     Dealt from seat 2, so the offer continues at the lowest remaining seat.
	///     The small blind pays for nearest, which makes the old furthest card the new nearest.
	///     The cutoff pays for the deck. The unmatched five chips of that toll are returned at showdown,
	///     so the small blind is awarded 85. Later streets are free.
	/// </summary>
	public static TollboothHandScript PaidChoices { get; } = new()
	{
		Name = "Paid choices",
		Holdings = Hands(
			Holes("8d 9d", "Kh", fourth: "4c", fifth: "9c", sixth: "Ad", seventh: "2s"),
			Holes("Ah 5h", "9h", fourth: "As", fifth: "6c", sixth: "Jc", seventh: "Qd"),
			Holes("3d 4h", "2c", fourth: "5c", fifth: "Tc", sixth: "Kd", seventh: "3h"),
			Holes("4d 5d", "Jh", fourth: "3s", fifth: "7c", sixth: "Qc", seventh: "2d"),
			Holes("6d 7d", "Th", fourth: "Qh", fifth: "8c", sixth: "Kc", seventh: "2h")),
		Steps =
		[
			..BringInCalls(Doors("Kh", "9h", "2c", "Jh", "Th"),
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			TollboothStep.Choose(HoldEmPosition.SmallBlind, TollboothStreet.Fourth, TollboothSource.Nearest,
				"As", "3s", 80, "As", "Ks"),
			TollboothStep.Choose(HoldEmPosition.UnderTheGun, TollboothStreet.Fourth, TollboothSource.Furthest,
				"3s", "4c", 80, "Ks", "3s"),
			TollboothStep.Choose(HoldEmPosition.Cutoff, TollboothStreet.Fourth, TollboothSource.Deck,
				"Qh", null, 90, "Ks", "4c"),
			TollboothStep.Choose(HoldEmPosition.Button, TollboothStreet.Fourth, TollboothSource.Furthest,
				"4c", "5c", 90, "Ks", "4c"),
			TollboothStep.Choose(HoldEmPosition.BigBlind, TollboothStreet.Fourth, TollboothSource.Furthest,
				"5c", "6c", 90, "Ks", "5c"),
			..Checks(TollboothStreet.Fourth, Boards("Kh 4c", "9h As", "2c 5c", "Jh 3s", "Th Qh"),
				HoldEmPosition.SmallBlind,
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button),
			..Furthest(TollboothStreet.Fifth, "Ks", 90, "Jc",
				(HoldEmPosition.SmallBlind, "6c"),
				(HoldEmPosition.UnderTheGun, "7c"),
				(HoldEmPosition.Cutoff, "8c"),
				(HoldEmPosition.Button, "9c"),
				(HoldEmPosition.BigBlind, "Tc")),
			..Checks(TollboothStreet.Fifth, Boards("Kh 4c 9c", "9h As 6c", "2c 5c Tc", "Jh 3s 7c", "Th Qh 8c"),
				HoldEmPosition.SmallBlind,
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button),
			..Furthest(TollboothStreet.Sixth, "Ks", 90, "Qd",
				(HoldEmPosition.SmallBlind, "Jc"),
				(HoldEmPosition.UnderTheGun, "Qc"),
				(HoldEmPosition.Cutoff, "Kc"),
				(HoldEmPosition.Button, "Ad"),
				(HoldEmPosition.BigBlind, "Kd")),
			..Checks(TollboothStreet.Sixth, Boards(
					"Kh 4c 9c Ad", "9h As 6c Jc", "2c 5c Tc Kd", "Jh 3s 7c Qc", "Th Qh 8c Kc"),
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind,
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff),
			..Furthest(TollboothStreet.Seventh, "Ks", 90, "3c",
				(HoldEmPosition.SmallBlind, "Qd"),
				(HoldEmPosition.UnderTheGun, "2d"),
				(HoldEmPosition.Cutoff, "2h"),
				(HoldEmPosition.Button, "2s"),
				(HoldEmPosition.BigBlind, "3h")),
			..Checks(TollboothStreet.Seventh, Boards(
					"Kh 4c 9c Ad", "9h As 6c Jc", "2c 5c Tc Kd", "Jh 3s 7c Qc", "Th Qh 8c Kc"),
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind,
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 65,
			[HoldEmPosition.Cutoff] = -20,
			[HoldEmPosition.Button] = -15,
			[HoldEmPosition.BigBlind] = -15,
			[HoldEmPosition.UnderTheGun] = -15
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 85,
		InitialNearest = "As",
		InitialFurthest = "Ks",
		ShowdownNearest = "Ks",
		ShowdownFurthest = "3c",
		OpeningPot = OpeningPot,
		WonByFold = false
	};

	/// <summary>
	///     The big blind bets fourth street, and the small blind folds that bet.
	///     Later offers skip the small blind. Fifth street is a big bet and one raise.
	///     The under-the-gun player wins by fold.
	/// </summary>
	public static TollboothHandScript FoldOnFourth { get; } = new()
	{
		Name = "Fold on fourth street",
		Holdings = Hands(
			Holes("Jc Qc", "8h", fourth: "7s", fifth: "9d"),
			Holes("5h 6h", "9h", fourth: "4s"),
			Holes("3d 4h", "2c", fourth: "Kh", fifth: "Kd"),
			Holes("4d 5d", "Jh", fourth: "5s", fifth: "9c"),
			Holes("6d 7d", "Th", fourth: "6s", fifth: "8c")),
		Steps =
		[
			..BringInCalls(Doors("8h", "9h", "2c", "Jh", "Th"),
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..Furthest(TollboothStreet.Fourth, "3c", CalledPot, "Kd",
				(HoldEmPosition.SmallBlind, "4s"),
				(HoldEmPosition.BigBlind, "Kh"),
				(HoldEmPosition.UnderTheGun, "5s"),
				(HoldEmPosition.Cutoff, "6s"),
				(HoldEmPosition.Button, "7s")),
			TollboothStep.Bet(
				HoldEmPosition.BigBlind,
				TollboothStreet.Fourth,
				SmallBet,
				85,
				Boards("8h 7s", "9h 4s", "2c Kh", "Jh 5s", "Th 6s")),
			TollboothStep.Call(HoldEmPosition.UnderTheGun, TollboothStreet.Fourth, SmallBet, 95, SmallBet),
			TollboothStep.Call(HoldEmPosition.Cutoff, TollboothStreet.Fourth, SmallBet, 105, SmallBet),
			TollboothStep.Call(HoldEmPosition.Button, TollboothStreet.Fourth, SmallBet, 115, SmallBet),
			TollboothStep.Fold(HoldEmPosition.SmallBlind, TollboothStreet.Fourth),
			..Furthest(TollboothStreet.Fifth, "3c", 115, "Ts",
				(HoldEmPosition.BigBlind, "Kd"),
				(HoldEmPosition.UnderTheGun, "9c"),
				(HoldEmPosition.Cutoff, "8c"),
				(HoldEmPosition.Button, "9d")),
			TollboothStep.Bet(
				HoldEmPosition.BigBlind,
				TollboothStreet.Fifth,
				BigBet,
				135,
				Boards("8h 7s 9d", "9h 4s", "2c Kh Kd", "Jh 5s 9c", "Th 6s 8c")),
			TollboothStep.Raise(HoldEmPosition.UnderTheGun, TollboothStreet.Fifth, BigBet * 2, 175),
			TollboothStep.Fold(HoldEmPosition.Cutoff, TollboothStreet.Fifth),
			TollboothStep.Fold(HoldEmPosition.Button, TollboothStreet.Fifth),
			TollboothStep.Fold(HoldEmPosition.BigBlind, TollboothStreet.Fifth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 110,
			[HoldEmPosition.BigBlind] = -45,
			[HoldEmPosition.Button] = -25,
			[HoldEmPosition.SmallBlind] = -15,
			[HoldEmPosition.Cutoff] = -25
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 175,
		InitialNearest = "3c",
		InitialFurthest = "4s",
		ShowdownNearest = "3c",
		ShowdownFurthest = "Ts",
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     Every card from fourth street on comes from the deck, so the two display aces stay up.
	///     The big blind's kings and queens win. Giving every hand those aces would make the
	///     under-the-gun three aces the winner instead.
	/// </summary>
	public static TollboothHandScript DisplayExcluded { get; } = new()
	{
		Name = "Display cards excluded",
		Holdings = Hands(
			Holes("Jc Qc", "8h", fourth: "7s", fifth: "6c", sixth: "Tc", seventh: "Td"),
			Holes("5h 6h", "9h", fourth: "4s", fifth: "2h", sixth: "7c", seventh: "Jd"),
			Holes("Kh Kd", "2c", fourth: "Qh", fifth: "Qd", sixth: "3s", seventh: "4d"),
			Holes("Ah 5d", "Jh", fourth: "5s", fifth: "4c", sixth: "8c", seventh: "8d"),
			Holes("6d 7d", "Th", fourth: "6s", fifth: "5c", sixth: "9c", seventh: "9d")),
		Steps =
		[
			..BringInCalls(Doors("8h", "9h", "2c", "Jh", "Th"),
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..TakeDeck(TollboothStreet.Fourth, "As", "Ad", CalledPot,
				(HoldEmPosition.SmallBlind, "4s"),
				(HoldEmPosition.BigBlind, "Qh"),
				(HoldEmPosition.UnderTheGun, "5s"),
				(HoldEmPosition.Cutoff, "6s"),
				(HoldEmPosition.Button, "7s")),
			..Checks(TollboothStreet.Fourth, Boards("8h 7s", "9h 4s", "2c Qh", "Jh 5s", "Th 6s"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..TakeDeck(TollboothStreet.Fifth, "As", "Ad", 125,
				(HoldEmPosition.SmallBlind, "2h"),
				(HoldEmPosition.BigBlind, "Qd"),
				(HoldEmPosition.UnderTheGun, "4c"),
				(HoldEmPosition.Cutoff, "5c"),
				(HoldEmPosition.Button, "6c")),
			..Checks(TollboothStreet.Fifth, Boards("8h 7s 6c", "9h 4s 2h", "2c Qh Qd", "Jh 5s 4c", "Th 6s 5c"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..TakeDeck(TollboothStreet.Sixth, "As", "Ad", 175,
				(HoldEmPosition.SmallBlind, "7c"),
				(HoldEmPosition.BigBlind, "3s"),
				(HoldEmPosition.UnderTheGun, "8c"),
				(HoldEmPosition.Cutoff, "9c"),
				(HoldEmPosition.Button, "Tc")),
			..Checks(TollboothStreet.Sixth, Boards(
					"8h 7s 6c Tc", "9h 4s 2h 7c", "2c Qh Qd 3s", "Jh 5s 4c 8c", "Th 6s 5c 9c"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind),
			..TakeDeck(TollboothStreet.Seventh, "As", "Ad", 225,
				(HoldEmPosition.SmallBlind, "Jd"),
				(HoldEmPosition.BigBlind, "4d"),
				(HoldEmPosition.UnderTheGun, "8d"),
				(HoldEmPosition.Cutoff, "9d"),
				(HoldEmPosition.Button, "Td")),
			..Checks(TollboothStreet.Seventh, Boards(
					"8h 7s 6c Tc", "9h 4s 2h 7c", "2c Qh Qd 3s", "Jh 5s 4c 8c", "Th 6s 5c 9c"),
				HoldEmPosition.BigBlind,
				HoldEmPosition.UnderTheGun,
				HoldEmPosition.Cutoff,
				HoldEmPosition.Button,
				HoldEmPosition.SmallBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 220,
			[HoldEmPosition.Button] = -55,
			[HoldEmPosition.SmallBlind] = -55,
			[HoldEmPosition.UnderTheGun] = -55,
			[HoldEmPosition.Cutoff] = -55
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 275,
		InitialNearest = "As",
		InitialFurthest = "Ad",
		ShowdownNearest = "As",
		ShowdownFurthest = "Ad",
		OpeningPot = OpeningPot,
		WonByFold = false
	};

	public static IReadOnlyList<TollboothHandScript> Orbit { get; } =
	[
		FoldToBringIn,
		FreeCheckdown,
		PaidChoices,
		FoldOnFourth,
		DisplayExcluded
	];

	private static TollboothStep[] BringInCalls(
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> doors,
		params HoldEmPosition[] actors)
	{
		var steps = new TollboothStep[actors.Length];
		var pot = OpeningPot;
		for (var index = 0; index < actors.Length; index++)
		{
			pot += BringInAmount;
			var closesTheStreet = index == actors.Length - 1;
			steps[index] = TollboothStep.Call(
				actors[index],
				TollboothStreet.Third,
				BringInAmount,
				pot,
				closesTheStreet ? null : BringInAmount,
				index == 0 ? doors : null);
		}

		return steps;
	}

	private static TollboothStep[] Furthest(
		TollboothStreet street,
		string nearest,
		int pot,
		string closingFurthest,
		params (HoldEmPosition Actor, string Card)[] takes)
	{
		var steps = new TollboothStep[takes.Length];
		for (var index = 0; index < takes.Length; index++)
		{
			var replenishment = index + 1 < takes.Length ? takes[index + 1].Card : closingFurthest;
			steps[index] = TollboothStep.Choose(
				takes[index].Actor,
				street,
				TollboothSource.Furthest,
				takes[index].Card,
				replenishment,
				pot,
				nearest,
				takes[index].Card);
		}

		return steps;
	}

	private static TollboothStep[] TakeDeck(
		TollboothStreet street,
		string nearest,
		string furthest,
		int pot,
		params (HoldEmPosition Actor, string Card)[] takes)
	{
		var steps = new TollboothStep[takes.Length];
		for (var index = 0; index < takes.Length; index++)
		{
			pot += Ante * 2;
			steps[index] = TollboothStep.Choose(
				takes[index].Actor,
				street,
				TollboothSource.Deck,
				takes[index].Card,
				null,
				pot,
				nearest,
				furthest);
		}

		return steps;
	}

	private static TollboothStep[] Checks(
		TollboothStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> boards,
		params HoldEmPosition[] actors)
	{
		var steps = new TollboothStep[actors.Length];
		for (var index = 0; index < actors.Length; index++)
		{
			steps[index] = TollboothStep.Check(
				actors[index],
				street,
				index == 0 ? boards : null);
		}

		return steps;
	}

	private static Dictionary<HoldEmPosition, int> LoseAntes(HoldEmPosition winner, int winnerNet)
	{
		var nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		};
		nets[winner] = winnerNet;
		return nets;
	}

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Doors(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff) =>
		Boards(button, smallBlind, bigBlind, underTheGun, cutoff);

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Boards(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = Split(button),
			[HoldEmPosition.SmallBlind] = Split(smallBlind),
			[HoldEmPosition.BigBlind] = Split(bigBlind),
			[HoldEmPosition.UnderTheGun] = Split(underTheGun),
			[HoldEmPosition.Cutoff] = Split(cutoff)
		};

	private static string[] Split(string cards) =>
		cards.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static Dictionary<HoldEmPosition, TollboothHolding> Hands(
		TollboothHolding button,
		TollboothHolding smallBlind,
		TollboothHolding bigBlind,
		TollboothHolding underTheGun,
		TollboothHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static TollboothHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null)
	{
		var cards = hole.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return new TollboothHolding
		{
			FirstHole = cards[0],
			SecondHole = cards[1],
			Door = door,
			Fourth = fourth,
			Fifth = fifth,
			Sixth = sixth,
			Seventh = seventh
		};
	}
}
