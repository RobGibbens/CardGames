using CardGames.Poker.Api.Features.Chat;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class ChatServiceTests
{
    private readonly ChatService _chatService;
    private readonly IChatMessageValidator _validator;
    private readonly ILogger<ChatService> _logger;

    public ChatServiceTests()
    {
        _logger = Substitute.For<ILogger<ChatService>>();
        _validator = new ChatMessageValidator();
        _chatService = new ChatService(_logger, _validator);
    }

    [Fact]
    public async Task SendMessage_ValidMessage_ReturnsMessage()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var senderName = "TestPlayer";
        var content = "Hello, world!";

        // Act
        var (message, error) = await _chatService.SendMessageAsync(tableId, senderName, content);

        // Assert
        message.Should().NotBeNull();
        error.Should().BeNull();
        message!.TableId.Should().Be(tableId);
        message.SenderName.Should().Be(senderName);
        message.Content.Should().Be(content);
        message.MessageType.Should().Be(ChatMessageType.Player);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsError()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var senderName = "TestPlayer";
        var content = "";

        // Act
        var (message, error) = await _chatService.SendMessageAsync(tableId, senderName, content);

        // Assert
        message.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendMessage_WhenChatDisabled_ReturnsError()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var senderName = "TestPlayer";
        var content = "Hello!";
        await _chatService.SetTableChatEnabledAsync(tableId, false);

        // Act
        var (message, error) = await _chatService.SendMessageAsync(tableId, senderName, content);

        // Assert
        message.Should().BeNull();
        error.Should().Be("Chat is disabled for this table.");
    }

    [Fact]
    public async Task GetChatHistory_ReturnsMessagesInOrder()
    {
        // Arrange - Use different senders to avoid rate limiting
        var tableId = Guid.NewGuid();
        await _chatService.SendMessageAsync(tableId, "Player1", "Message 1");
        await _chatService.SendMessageAsync(tableId, "Player2", "Message 2");
        await _chatService.SendMessageAsync(tableId, "Player3", "Message 3");

        // Act
        var history = await _chatService.GetChatHistoryAsync(tableId);

        // Assert
        history.Should().HaveCount(3);
        history[0].Content.Should().Be("Message 1");
        history[1].Content.Should().Be("Message 2");
        history[2].Content.Should().Be("Message 3");
    }

    [Fact]
    public async Task CreateSystemAnnouncement_ReturnsSystemMessage()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var content = "Player joined the table";

        // Act
        var message = await _chatService.CreateSystemAnnouncementAsync(tableId, content);

        // Assert
        message.Should().NotBeNull();
        message.TableId.Should().Be(tableId);
        message.Content.Should().Be(content);
        message.MessageType.Should().Be(ChatMessageType.System);
        message.SenderName.Should().Be("System");
    }

    [Fact]
    public async Task MutePlayer_Success()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var playerToMute = "Player2";

        // Act
        var (success, error) = await _chatService.MutePlayerAsync(tableId, playerName, playerToMute);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task MutePlayer_CannotMuteSelf()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";

        // Act
        var (success, error) = await _chatService.MutePlayerAsync(tableId, playerName, playerName);

        // Assert
        success.Should().BeFalse();
        error.Should().Be("You cannot mute yourself.");
    }

    [Fact]
    public async Task MutePlayer_AlreadyMuted_ReturnsError()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var playerToMute = "Player2";
        await _chatService.MutePlayerAsync(tableId, playerName, playerToMute);

        // Act
        var (success, error) = await _chatService.MutePlayerAsync(tableId, playerName, playerToMute);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("already muted");
    }

    [Fact]
    public async Task UnmutePlayer_Success()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var playerToUnmute = "Player2";
        await _chatService.MutePlayerAsync(tableId, playerName, playerToUnmute);

        // Act
        var (success, error) = await _chatService.UnmutePlayerAsync(tableId, playerName, playerToUnmute);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task UnmutePlayer_NotMuted_ReturnsError()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var playerToUnmute = "Player2";

        // Act
        var (success, error) = await _chatService.UnmutePlayerAsync(tableId, playerName, playerToUnmute);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("not muted");
    }

    [Fact]
    public async Task GetMutedPlayers_ReturnsMutedList()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        await _chatService.MutePlayerAsync(tableId, playerName, "Player2");
        await _chatService.MutePlayerAsync(tableId, playerName, "Player3");

        // Act
        var mutedPlayers = await _chatService.GetMutedPlayersAsync(tableId, playerName);

        // Assert
        mutedPlayers.Should().HaveCount(2);
        mutedPlayers.Should().Contain("Player2");
        mutedPlayers.Should().Contain("Player3");
    }

    [Fact]
    public async Task IsPlayerMuted_ReturnsTrueWhenMuted()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var mutedPlayer = "Player2";
        await _chatService.MutePlayerAsync(tableId, playerName, mutedPlayer);

        // Act
        var isMuted = await _chatService.IsPlayerMutedAsync(tableId, playerName, mutedPlayer);

        // Assert
        isMuted.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlayerMuted_ReturnsFalseWhenNotMuted()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var playerName = "Player1";
        var otherPlayer = "Player2";

        // Act
        var isMuted = await _chatService.IsPlayerMutedAsync(tableId, playerName, otherPlayer);

        // Assert
        isMuted.Should().BeFalse();
    }

    [Fact]
    public async Task SetTableChatEnabled_TogglesStatus()
    {
        // Arrange
        var tableId = Guid.NewGuid();

        // Act & Assert - Default should be enabled
        var isEnabled = await _chatService.IsTableChatEnabledAsync(tableId);
        isEnabled.Should().BeTrue();

        // Disable chat
        await _chatService.SetTableChatEnabledAsync(tableId, false);
        isEnabled = await _chatService.IsTableChatEnabledAsync(tableId);
        isEnabled.Should().BeFalse();

        // Re-enable chat
        await _chatService.SetTableChatEnabledAsync(tableId, true);
        isEnabled = await _chatService.IsTableChatEnabledAsync(tableId);
        isEnabled.Should().BeTrue();
    }
}

public class ChatMessageValidatorTests
{
    private readonly ChatMessageValidator _validator = new();

    [Fact]
    public void Validate_ValidMessage_ReturnsValid()
    {
        // Arrange
        var playerName = "TestPlayer";
        var content = "Hello, world!";

        // Act
        var (isValid, error) = _validator.Validate(playerName, content);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyContent_ReturnsInvalid(string? content)
    {
        // Arrange
        var playerName = "TestPlayer";

        // Act
        var (isValid, error) = _validator.Validate(playerName, content!);

        // Assert
        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_TooLongMessage_ReturnsInvalid()
    {
        // Arrange
        var playerName = "TestPlayer";
        var content = new string('a', 501); // Exceeds 500 char limit

        // Act
        var (isValid, error) = _validator.Validate(playerName, content);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("maximum length");
    }

    [Fact]
    public void Validate_ExcessiveCaps_ReturnsInvalid()
    {
        // Arrange
        var playerName = "TestPlayer";
        var content = "THIS IS ALL CAPS MESSAGE"; // More than 70% uppercase with > 10 chars

        // Act
        var (isValid, error) = _validator.Validate(playerName, content);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("capital letters");
    }

    [Fact]
    public void Validate_RepeatedChars_ReturnsInvalid()
    {
        // Arrange
        var playerName = "TestPlayer";
        var content = "Hellooooooo"; // 5+ repeated chars

        // Act
        var (isValid, error) = _validator.Validate(playerName, content);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("repeated characters");
    }

    [Fact]
    public void Validate_NormalMixedCase_ReturnsValid()
    {
        // Arrange
        var playerName = "TestPlayer";
        var content = "Nice Hand! GG everyone.";

        // Act
        var (isValid, error) = _validator.Validate(playerName, content);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }
}
