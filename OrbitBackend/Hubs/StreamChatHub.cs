using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrbitBackend.Services.Interfaces;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace OrbitBackend.Hubs
{
    /// <summary>
    /// SignalR hub for real-time stream chat.
    /// 
    /// Clients connect with JWT auth via query string:
    ///   /hubs/stream-chat?access_token={jwt}
    ///
    /// Client events to invoke:
    ///   - JoinStream(streamId)    → joins the chat room
    ///   - LeaveStream(streamId)   → leaves the chat room
    ///   - SendMessage(streamId, content) → sends a message (auth required)
    ///
    /// Server events to listen for:
    ///   - ReceiveMessage(message) → broadcast to all users in the room
    ///   - Error(message)          → error notification
    /// </summary>
    [Authorize]
    public class StreamChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<StreamChatHub> _logger;

        // Simple in-memory rate limiter: userId → list of send timestamps
        private static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimits = new();
        private const int MaxMessagesPerWindow = 5;
        private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

        public StreamChatHub(IChatService chatService, ILogger<StreamChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>
        /// Adds the caller to the SignalR group for a specific stream.
        /// </summary>
        public async Task JoinStream(int streamId)
        {
            var groupName = GetGroupName(streamId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug(
                "Connection {ConnectionId} joined stream group {Group}.",
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Removes the caller from the SignalR group for a specific stream.
        /// </summary>
        public async Task LeaveStream(int streamId)
        {
            var groupName = GetGroupName(streamId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug(
                "Connection {ConnectionId} left stream group {Group}.",
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Sends a chat message to a live stream.
        /// Requires authentication. Rate-limited to 5 messages per 10 seconds.
        /// </summary>
        public async Task SendMessage(int streamId, string content)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "You must be authenticated to send messages.");
                return;
            }

            // Validate content
            if (string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("Error", "Message cannot be empty.");
                return;
            }

            if (content.Length > 500)
            {
                await Clients.Caller.SendAsync("Error", "Message cannot exceed 500 characters.");
                return;
            }

            // Rate limiting
            if (!CheckRateLimit(userId))
            {
                await Clients.Caller.SendAsync("Error", "You are sending messages too quickly. Please wait a moment.");
                return;
            }

            try
            {
                // Persist and get the DTO back
                var messageDto = await _chatService.SaveMessageAsync(streamId, userId, content);

                // Broadcast to all users watching this stream
                var groupName = GetGroupName(streamId);
                await Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);
            }
            catch (InvalidOperationException ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Clean up rate limit entry when user disconnects
            var userId = GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                _rateLimits.TryRemove(userId, out _);
            }

            return base.OnDisconnectedAsync(exception);
        }

        private string? GetUserId()
        {
            return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Context.User?.FindFirstValue("sub");
        }

        private static string GetGroupName(int streamId) => $"stream_{streamId}";

        /// <summary>
        /// Sliding-window rate limiter. Returns true if the message is allowed.
        /// </summary>
        private static bool CheckRateLimit(string userId)
        {
            var now = DateTime.UtcNow;
            var timestamps = _rateLimits.GetOrAdd(userId, _ => new List<DateTime>());

            lock (timestamps)
            {
                // Remove expired entries
                timestamps.RemoveAll(t => now - t > RateWindow);

                if (timestamps.Count >= MaxMessagesPerWindow)
                    return false;

                timestamps.Add(now);
                return true;
            }
        }
    }
}
