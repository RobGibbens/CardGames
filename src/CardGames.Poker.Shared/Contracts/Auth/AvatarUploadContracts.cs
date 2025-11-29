namespace CardGames.Poker.Shared.Contracts.Auth;

public record AvatarUploadRequest(
    string ImageDataUrl
);

public record AvatarUploadResponse(
    bool Success,
    string? AvatarUrl = null,
    string? Error = null
);
