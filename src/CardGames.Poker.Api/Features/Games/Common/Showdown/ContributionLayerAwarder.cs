using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.Showdown;

/// <summary>
///     Splits one hand's <see cref="GamePlayer.TotalContributedThisHand" /> totals the way
///     <see cref="HoldEmPotLayers.Build" /> does, then pays each kept layer to the best hand
///     that is allowed to win it.
/// </summary>
public sealed class ContributionLayerAwarder(CardsDbContext context)
{
	public async Task<OneOf<ContributionLayerAwardResult, string>> AwardAsync(
		Game game,
		IReadOnlyList<Pot> currentHandPots,
		DateTimeOffset now,
		bool isAlreadyAwarded,
		IReadOnlyDictionary<Guid, long> strengthByGamePlayerId,
		Func<GamePlayer, bool> canWin,
		Func<Guid, string> handType,
		CancellationToken cancellationToken,
		bool awardKeptPots = true)
	{
		if (isAlreadyAwarded)
			return Reconstruct(currentHandPots);

		var potTotal = currentHandPots.Sum(pot => pot.Amount);
		var contributionSum = game.GamePlayers.Sum(gamePlayer => gamePlayer.TotalContributedThisHand);
		var mismatch = ContributionMismatch(potTotal, contributionSum);
		if (mismatch is not null)
			return mismatch;

		var stakes = game.GamePlayers.Select(gamePlayer => new HoldEmPotLayers.Stake(
			gamePlayer.Id,
			gamePlayer.Player.Name,
			gamePlayer.TotalContributedThisHand,
			strengthByGamePlayerId.ContainsKey(gamePlayer.Id),
			strengthByGamePlayerId.ContainsKey(gamePlayer.Id) &&
			(gamePlayer.IsAllIn || gamePlayer.ChipStack == 0))).ToList();

		var layers = HoldEmPotLayers.Build(stakes);
		var layerError = ValidateLayers(layers, contributionSum);
		if (layerError is not null)
			return layerError;

		var (keptLayers, unwinnableRefunds) = ApplyCanWin(game, layers.Layers, canWin);
		var refunded = unwinnableRefunds.Values.Sum();
		var keptTotal = keptLayers.Sum(layer => layer.Amount);
		if (layers.Uncalled + refunded + keptTotal != contributionSum)
			return
				$"Uncalled chips, unwinnable refunds, and pot layers ({layers.Uncalled + refunded + keptTotal}) do not equal contributed chips ({contributionSum}).";

		if (layers.Uncalled > 0 && layers.UncalledPlayerId is Guid uncalledPlayerId)
			Refund(game, uncalledPlayerId, layers.Uncalled);

		foreach (var (gamePlayerId, amount) in unwinnableRefunds)
			Refund(game, gamePlayerId, amount);

		var pots = await ReplaceCurrentHandPotsAsync(game, currentHandPots, keptLayers, now, cancellationToken);
		var payouts = new Dictionary<string, int>();
		var winnerNames = new HashSet<string>();
		string? winReason = null;

		if (awardKeptPots)
		{
			foreach (var pot in pots.OrderBy(pot => pot.PotOrder))
			{
				var reason = AwardPot(game, pot, strengthByGamePlayerId, handType, payouts, winnerNames, now);
				if (pot.PotOrder == 0)
					winReason = reason;
			}

			foreach (var payout in payouts)
			{
				var gamePlayer = game.GamePlayers.First(player => player.Player.Name == payout.Key);
				gamePlayer.ChipStack += payout.Value;
			}
		}

		return new ContributionLayerAwardResult(payouts, winnerNames, winReason, pots.Sum(pot => pot.Amount), pots);
	}

	public static string? ContributionMismatch(int potTotal, int contributionSum)
	{
		if (potTotal == contributionSum)
			return null;

		return $"Pot total {potTotal} does not equal contributed chips {contributionSum}.";
	}

	private static (List<HoldEmPotLayers.Layer> Kept, Dictionary<Guid, int> Refunds) ApplyCanWin(
		Game game,
		IReadOnlyList<HoldEmPotLayers.Layer> layers,
		Func<GamePlayer, bool> canWin)
	{
		var kept = new List<HoldEmPotLayers.Layer>();
		var refunds = new Dictionary<Guid, int>();

		foreach (var layer in layers.OrderBy(layer => layer.PotOrder))
		{
			var slices = layer.Slices.Select(slice =>
			{
				var gamePlayer = game.GamePlayers.First(player => player.Id == slice.GamePlayerId);
				return slice with { Eligible = slice.Eligible && canWin(gamePlayer) };
			}).ToList();

			if (slices.Any(slice => slice.Eligible))
			{
				kept.Add(layer with { Slices = slices });
				continue;
			}

			foreach (var slice in slices)
			{
				if (slice.Amount == 0)
					continue;

				refunds[slice.GamePlayerId] = refunds.GetValueOrDefault(slice.GamePlayerId) + slice.Amount;
			}
		}

		return (kept, refunds);
	}

	private static void Refund(Game game, Guid gamePlayerId, int amount)
	{
		if (amount <= 0)
			return;

		var gamePlayer = game.GamePlayers.First(player => player.Id == gamePlayerId);
		gamePlayer.ChipStack += amount;
		gamePlayer.TotalContributedThisHand -= amount;
	}

	private static string? ValidateLayers(HoldEmPotLayers.BuildResult layers, int contributedSum)
	{
		var layered = layers.Uncalled + layers.Layers.Sum(layer => layer.Amount);
		if (layered != contributedSum)
			return
				$"Uncalled chips plus pot layers ({layered}) do not equal contributed chips ({contributedSum}).";

		foreach (var layer in layers.Layers)
		{
			var sliceSum = layer.Slices.Sum(slice => slice.Amount);
			if (sliceSum != layer.Amount)
				return $"Layer {layer.PotOrder} slices total {sliceSum} but the layer amount is {layer.Amount}.";

			if (layer.Amount <= 0 || !layer.Slices.Any(slice => slice.Eligible))
				return $"Layer {layer.PotOrder} amount {layer.Amount} has no eligible winner.";
		}

		return null;
	}

	private async Task<List<Pot>> ReplaceCurrentHandPotsAsync(
		Game game,
		IReadOnlyList<Pot> currentHandPots,
		IReadOnlyList<HoldEmPotLayers.Layer> layers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var potIds = currentHandPots.Select(pot => pot.Id).ToList();
		var existingContributions = await context.PotContributions
			.Where(contribution => potIds.Contains(contribution.PotId))
			.ToListAsync(cancellationToken);
		context.PotContributions.RemoveRange(existingContributions);

		foreach (var pot in currentHandPots)
		{
			context.Pots.Remove(pot);
			game.Pots.Remove(pot);
		}

		var newPots = new List<Pot>();
		for (var index = 0; index < layers.Count; index++)
		{
			var layer = layers[index];
			var pot = new Pot
			{
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				PotOrder = index,
				PotType = index == 0 ? PotType.Main : PotType.Side,
				Amount = layer.Amount,
				MaxContributionPerPlayer = layer.MaxContributionPerPlayer,
				CreatedAt = now,
				IsAwarded = false
			};

			foreach (var slice in layer.Slices.Where(slice => slice.Amount > 0 || slice.Eligible))
			{
				var gamePlayer = game.GamePlayers.First(player => player.Id == slice.GamePlayerId);
				pot.Contributions.Add(new PotContribution
				{
					Pot = pot,
					GamePlayerId = slice.GamePlayerId,
					GamePlayer = gamePlayer,
					Amount = slice.Amount,
					IsEligibleToWin = slice.Eligible,
					ContributedAt = now
				});
			}

			context.Pots.Add(pot);
			newPots.Add(pot);
		}

		return newPots;
	}

	private static string? AwardPot(
		Game game,
		Pot pot,
		IReadOnlyDictionary<Guid, long> strengthByGamePlayerId,
		Func<Guid, string> handType,
		Dictionary<string, int> payouts,
		HashSet<string> winnerNames,
		DateTimeOffset now)
	{
		if (pot.Amount == 0)
			return null;

		var eligible = pot.Contributions
			.Where(contribution =>
				contribution.IsEligibleToWin &&
				strengthByGamePlayerId.ContainsKey(contribution.GamePlayerId))
			.ToList();
		if (eligible.Count == 0)
			return null;

		var maxStrength = eligible.Max(contribution => strengthByGamePlayerId[contribution.GamePlayerId]);
		var potWinners = eligible
			.Where(contribution => strengthByGamePlayerId[contribution.GamePlayerId] == maxStrength)
			.ToList();
		var orderedWinners = OddChipOrder.OrderClockwiseFromDealer(
			potWinners,
			game.DealerPosition,
			contribution => contribution.GamePlayer.SeatPosition);
		var shares = OddChipOrder.Distribute(pot.Amount, orderedWinners);
		var potPayouts = new List<(GamePlayer GamePlayer, int Amount)>();

		for (var index = 0; index < orderedWinners.Count; index++)
		{
			var winner = orderedWinners[index];
			var payout = shares[index];
			var name = winner.GamePlayer.Player.Name;
			payouts[name] = payouts.GetValueOrDefault(name) + payout;
			winnerNames.Add(name);
			potPayouts.Add((winner.GamePlayer, payout));
		}

		var winningHand = handType(orderedWinners[0].GamePlayerId);
		var winReason = orderedWinners.Count > 1 ? $"Split pot - {winningHand}" : winningHand;
		pot.IsAwarded = true;
		pot.AwardedAt = now;
		pot.WinReason = winReason;
		pot.WinnerPayouts = JsonSerializer.Serialize(potPayouts.Select(payout => new
		{
			playerId = payout.GamePlayer.PlayerId.ToString(),
			playerName = payout.GamePlayer.Player.Name,
			amount = payout.Amount,
			highHandAmount = 0,
			showcaseAmount = 0
		}));

		return winReason;
	}

	private static ContributionLayerAwardResult Reconstruct(IReadOnlyList<Pot> currentHandPots)
	{
		var payouts = new Dictionary<string, int>();
		var winnerNames = new HashSet<string>();

		foreach (var pot in currentHandPots.Where(pot => pot.WinnerPayouts != null))
			try
			{
				var potPayouts = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(pot.WinnerPayouts!);
				if (potPayouts is null)
					continue;

				foreach (var potPayout in potPayouts)
				{
					if (!potPayout.TryGetValue("playerName", out var nameObj) ||
					    !potPayout.TryGetValue("amount", out var amountObj))
						continue;

					var name = nameObj.ToString()!;
					var amount = Convert.ToInt32(((JsonElement)amountObj).GetInt32());
					payouts[name] = payouts.GetValueOrDefault(name) + amount;
					winnerNames.Add(name);
				}
			}
			catch
			{
				// Ignore parsing errors for legacy data
			}

		var winReason = currentHandPots.OrderBy(pot => pot.PotOrder).FirstOrDefault()?.WinReason;
		return new ContributionLayerAwardResult(
			payouts,
			winnerNames,
			winReason,
			currentHandPots.Sum(pot => pot.Amount),
			currentHandPots);
	}
}

public sealed record ContributionLayerAwardResult(
	Dictionary<string, int> Payouts,
	HashSet<string> WinnerNames,
	string? WinReason,
	int HandHistoryPot,
	IReadOnlyList<Pot> Pots);
