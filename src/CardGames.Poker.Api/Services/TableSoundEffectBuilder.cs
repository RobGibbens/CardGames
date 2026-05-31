using System.Security.Cryptography;
using System.Text;
using CardGames.Contracts.SignalR;
using Entities = CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Projects deterministic table sound-effect cues (e.g. periodic "winning" soundboard clips)
/// from the public showdown result, keeping presentation concerns out of the core state flow.
/// </summary>
internal static class TableSoundEffectBuilder
{
	private static readonly Dictionary<string, string[]> TableSoundboardFiles =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["winning"] = ["pay_dat_man_his_money.mp3"]
		};

	private const int WinningSoundFrequencyHands = 10;

	internal static IReadOnlyList<TableSoundEffectDto>? Build(Entities.Game game, ShowdownPublicDto? showdown)
	{
		if (game.CurrentHandNumber <= 0 || game.CurrentHandNumber % WinningSoundFrequencyHands != 0 || showdown is not { IsComplete: true })
		{
			return null;
		}

		var hasWinner = showdown.PlayerResults.Any(result => result.IsWinner || result.AmountWon > 0);
		if (!hasWinner)
		{
			return null;
		}

		var source = ChooseDeterministicSoundboardSource(game.Id, game.CurrentHandNumber, "winning");
		if (string.IsNullOrWhiteSpace(source))
		{
			return null;
		}

		return
		[
			new TableSoundEffectDto
			{
				CueKey = $"winning:{game.CurrentHandNumber}:{Path.GetFileName(source)}",
				EventKey = "winning",
				HandNumber = game.CurrentHandNumber,
				Source = source
			}
		];
	}

	private static string? ChooseDeterministicSoundboardSource(Guid gameId, int handNumber, string eventKey)
	{
		if (!TableSoundboardFiles.TryGetValue(eventKey, out var files) || files.Length == 0)
		{
			return null;
		}

		var seedBytes = Encoding.UTF8.GetBytes($"{gameId:N}:{handNumber}:{eventKey}");
		var hashBytes = SHA256.HashData(seedBytes);
		var selectedIndex = BitConverter.ToUInt32(hashBytes, 0) % (uint)files.Length;
		var fileName = files[(int)selectedIndex];
		return $"/sounds/soundboard/{eventKey}/{Uri.EscapeDataString(fileName)}";
	}
}
