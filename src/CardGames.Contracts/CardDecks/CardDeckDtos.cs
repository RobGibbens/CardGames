namespace CardGames.Poker.Api.Contracts;

public sealed record CardDeckSummaryDto
{
	public Guid? Id { get; init; }

	public required string Name { get; init; }

	public required bool IsBuiltIn { get; init; }

	public required bool IsComplete { get; init; }

	public required bool IsSelected { get; init; }

	public required int UploadedFaceCount { get; init; }

	public required bool HasBack { get; init; }
}

public sealed record CardDeckListDto
{
	public required IReadOnlyList<CardDeckSummaryDto> Decks { get; init; }
}

public sealed record CreateCardDeckRequest
{
	public required string Name { get; init; }
}

public sealed record RenameCardDeckRequest
{
	public required string Name { get; init; }
}

public sealed record SelectCardDeckRequest
{
	public Guid? DeckId { get; init; }
}

public sealed record CardDeckImageSlotDto
{
	public required string SlotKey { get; init; }

	public string? Suit { get; init; }

	public string? Rank { get; init; }

	public required bool IsBack { get; init; }

	public string? ImageUrl { get; init; }
}

public sealed record CardDeckDetailDto
{
	public required Guid Id { get; init; }

	public required string Name { get; init; }

	public required bool IsComplete { get; init; }

	public required int UploadedFaceCount { get; init; }

	public required bool HasBack { get; init; }

	public required IReadOnlyList<CardDeckImageSlotDto> Slots { get; init; }
}

public sealed record SelectedCardDeckImagesDto
{
	public Guid? DeckId { get; init; }

	public required string Name { get; init; }

	public required string BackUrl { get; init; }

	public required IReadOnlyDictionary<string, string> Faces { get; init; }
}
