using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Shared.Contracts.Chat;
using CardGames.Poker.Shared.Events;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Features.Chat;

public static class ChatModule
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat")
            .WithTags("Chat");

        group.MapPost("{tableId:guid}/messages", SendMessageAsync)
            .WithName("SendChatMessage");

        group.MapGet("{tableId:guid}/messages", GetChatHistoryAsync)
            .WithName("GetChatHistory");

        group.MapPost("{tableId:guid}/mute", MutePlayerAsync)
            .WithName("MutePlayer");

        group.MapPost("{tableId:guid}/unmute", UnmutePlayerAsync)
            .WithName("UnmutePlayer");

        group.MapGet("{tableId:guid}/muted", GetMutedPlayersAsync)
            .WithName("GetMutedPlayers");

        group.MapPost("{tableId:guid}/status", SetChatStatusAsync)
            .WithName("SetChatStatus");

        group.MapGet("{tableId:guid}/status", GetChatStatusAsync)
            .WithName("GetChatStatus");

        return app;
    }

    private static async Task<IResult> SendMessageAsync(
        Guid tableId,
        SendChatMessageRequest request,
        IChatService chatService,
        IConnectionMappingService connectionMapping,
        IHubContext<GameHub> hubContext,
        HttpContext httpContext)
    {
        // Get player name from connection (in a real app, this would come from auth)
        // For now, we'll require it in the request or get from a header
        var playerName = httpContext.Request.Headers["X-Player-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Results.BadRequest(new SendChatMessageResponse(false, Error: "Player name is required."));
        }

        if (tableId != request.TableId)
        {
            return Results.BadRequest(new SendChatMessageResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (message, error) = await chatService.SendMessageAsync(tableId, playerName, request.Content);

        if (message == null)
        {
            // If rate limited or validation failed, send rejection event to the player
            var connectionId = connectionMapping.GetConnectionId(playerName, tableId.ToString());
            if (connectionId != null)
            {
                var rejectionEvent = new ChatMessageRejectedEvent(tableId, DateTime.UtcNow, playerName, error!);
                await hubContext.Clients.Client(connectionId).SendAsync("ChatMessageRejected", rejectionEvent);
            }
            return Results.BadRequest(new SendChatMessageResponse(false, Error: error));
        }

        // Broadcast the message to the table
        var chatEvent = new ChatMessageSentEvent(tableId, DateTime.UtcNow, message);
        await hubContext.Clients.Group(tableId.ToString()).SendAsync("ChatMessageReceived", chatEvent);

        return Results.Ok(new SendChatMessageResponse(true, Message: message));
    }

    private static async Task<IResult> GetChatHistoryAsync(
        Guid tableId,
        IChatService chatService,
        int maxMessages = 50)
    {
        var messages = await chatService.GetChatHistoryAsync(tableId, maxMessages);
        return Results.Ok(new GetChatHistoryResponse(true, Messages: messages));
    }

    private static async Task<IResult> MutePlayerAsync(
        Guid tableId,
        MutePlayerRequest request,
        IChatService chatService,
        IConnectionMappingService connectionMapping,
        IHubContext<GameHub> hubContext,
        HttpContext httpContext)
    {
        var playerName = httpContext.Request.Headers["X-Player-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Results.BadRequest(new MutePlayerResponse(false, Error: "Player name is required."));
        }

        if (tableId != request.TableId)
        {
            return Results.BadRequest(new MutePlayerResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, error) = await chatService.MutePlayerAsync(tableId, playerName, request.PlayerToMute);

        if (!success)
        {
            return Results.BadRequest(new MutePlayerResponse(false, Error: error));
        }

        // Notify the player that they muted someone
        var connectionId = connectionMapping.GetConnectionId(playerName, tableId.ToString());
        if (connectionId != null)
        {
            var muteEvent = new PlayerMutedEvent(tableId, DateTime.UtcNow, playerName, request.PlayerToMute);
            await hubContext.Clients.Client(connectionId).SendAsync("PlayerMuted", muteEvent);
        }

        return Results.Ok(new MutePlayerResponse(true));
    }

    private static async Task<IResult> UnmutePlayerAsync(
        Guid tableId,
        UnmutePlayerRequest request,
        IChatService chatService,
        IConnectionMappingService connectionMapping,
        IHubContext<GameHub> hubContext,
        HttpContext httpContext)
    {
        var playerName = httpContext.Request.Headers["X-Player-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Results.BadRequest(new UnmutePlayerResponse(false, Error: "Player name is required."));
        }

        if (tableId != request.TableId)
        {
            return Results.BadRequest(new UnmutePlayerResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, error) = await chatService.UnmutePlayerAsync(tableId, playerName, request.PlayerToUnmute);

        if (!success)
        {
            return Results.BadRequest(new UnmutePlayerResponse(false, Error: error));
        }

        // Notify the player that they unmuted someone
        var connectionId = connectionMapping.GetConnectionId(playerName, tableId.ToString());
        if (connectionId != null)
        {
            var unmuteEvent = new PlayerUnmutedEvent(tableId, DateTime.UtcNow, playerName, request.PlayerToUnmute);
            await hubContext.Clients.Client(connectionId).SendAsync("PlayerUnmuted", unmuteEvent);
        }

        return Results.Ok(new UnmutePlayerResponse(true));
    }

    private static async Task<IResult> GetMutedPlayersAsync(
        Guid tableId,
        IChatService chatService,
        HttpContext httpContext)
    {
        var playerName = httpContext.Request.Headers["X-Player-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Results.BadRequest(new GetMutedPlayersResponse(false, Error: "Player name is required."));
        }

        var mutedPlayers = await chatService.GetMutedPlayersAsync(tableId, playerName);
        return Results.Ok(new GetMutedPlayersResponse(true, MutedPlayers: mutedPlayers));
    }

    private static async Task<IResult> SetChatStatusAsync(
        Guid tableId,
        SetTableChatStatusRequest request,
        IChatService chatService,
        IHubContext<GameHub> hubContext,
        HttpContext httpContext)
    {
        // In a real app, this would require table host/admin privileges
        var playerName = httpContext.Request.Headers["X-Player-Name"].FirstOrDefault();

        if (tableId != request.TableId)
        {
            return Results.BadRequest(new SetTableChatStatusResponse(false, false, Error: "Table ID in URL does not match request body."));
        }

        var (success, error) = await chatService.SetTableChatEnabledAsync(tableId, request.EnableChat, playerName);

        if (!success)
        {
            return Results.BadRequest(new SetTableChatStatusResponse(false, false, Error: error));
        }

        // Broadcast the status change to the table
        var statusEvent = new TableChatStatusChangedEvent(tableId, DateTime.UtcNow, request.EnableChat, playerName);
        await hubContext.Clients.Group(tableId.ToString()).SendAsync("TableChatStatusChanged", statusEvent);

        return Results.Ok(new SetTableChatStatusResponse(true, request.EnableChat));
    }

    private static async Task<IResult> GetChatStatusAsync(
        Guid tableId,
        IChatService chatService)
    {
        var isEnabled = await chatService.IsTableChatEnabledAsync(tableId);
        return Results.Ok(new SetTableChatStatusResponse(true, isEnabled));
    }
}
