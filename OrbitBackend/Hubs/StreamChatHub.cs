using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.Services;
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
    ///   - JoinStream(streamId)    → joins the chat room, increments viewer count
    ///   - LeaveStream(streamId)   → leaves the chat room, decrements viewer count
    ///   - SendMessage(streamId, content) → sends a message (auth required, ban/timeout checked)
    ///   - DeleteMessage(streamId, messageId) → deletes a message (mod/streamer only)
    ///
    /// Server events to listen for:
    ///   - ReceiveMessage(message) → broadcast to all users in the room
    ///   - MessageDeleted(messageId) → broadcast when a message is deleted by a mod
    ///   - ViewerCountUpdate(streamId, count) → broadcast when viewer count changes
    ///   - UserTimedOut(username, durationSeconds) → broadcast when a user is timed out
    ///   - UserBanned(username) → broadcast when a user is banned
    ///   - Error(message)          → error notification
    /// </summary>
    [Authorize]
    public class StreamChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly IModerationService _moderationService;
        private readonly ViewerTracker _viewerTracker;
        private readonly AppDbContext _context;
        private readonly ILogger<StreamChatHub> _logger;

        // Simple in-memory rate limiter: userId → list of send timestamps
        private static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimits = new();
        private const int MaxMessagesPerWindow = 5;
        private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

        // Track which stream each connection joined (for cleanup on disconnect)
        private static readonly ConcurrentDictionary<string, int> _connectionStreams = new();

        public StreamChatHub(
            IChatService chatService,
            IModerationService moderationService,
            ViewerTracker viewerTracker,
            AppDbContext context,
            ILogger<StreamChatHub> logger)
        {
            _chatService = chatService;
            _moderationService = moderationService;
            _viewerTracker = viewerTracker;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Adds the caller to the SignalR group for a specific stream and tracks viewer count.
        /// </summary>
        public async Task JoinStream(int streamId)
        {
            var groupName = GetGroupName(streamId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // Track viewer
            _viewerTracker.AddViewer(streamId, Context.ConnectionId);
            _connectionStreams[Context.ConnectionId] = streamId;

            // Broadcast updated viewer count
            var count = _viewerTracker.GetViewerCount(streamId);
            await Clients.Group(groupName).SendAsync("ViewerCountUpdate", streamId, count);

            _logger.LogDebug(
                "Connection {ConnectionId} joined stream group {Group}. Viewers: {Count}",
                Context.ConnectionId, groupName, count);
        }

        /// <summary>
        /// Removes the caller from the SignalR group for a specific stream and updates viewer count.
        /// </summary>
        public async Task LeaveStream(int streamId)
        {
            var groupName = GetGroupName(streamId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            // Remove viewer
            _viewerTracker.RemoveViewer(streamId, Context.ConnectionId);
            _connectionStreams.TryRemove(Context.ConnectionId, out _);

            // Broadcast updated viewer count
            var count = _viewerTracker.GetViewerCount(streamId);
            await Clients.Group(groupName).SendAsync("ViewerCountUpdate", streamId, count);

            _logger.LogDebug(
                "Connection {ConnectionId} left stream group {Group}. Viewers: {Count}",
                Context.ConnectionId, groupName, count);
        }

        /// <summary>
        /// Sends a chat message to a live stream.
        /// Requires authentication. Checks ban/timeout status. Rate-limited.
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

            // Get the channel for this stream to check moderation status
            var stream = await _context.LiveStreams
                .FirstOrDefaultAsync(s => s.Id == streamId);

            if (stream == null)
            {
                await Clients.Caller.SendAsync("Error", "Stream not found.");
                return;
            }

            // Check if user is banned
            if (await _moderationService.IsUserBannedAsync(stream.ChannelId, userId))
            {
                await Clients.Caller.SendAsync("Error", "You are banned from chatting in this channel.");
                return;
            }

            // Check if user is timed out
            if (await _moderationService.IsUserTimedOutAsync(stream.ChannelId, userId))
            {
                await Clients.Caller.SendAsync("Error", "You are currently timed out. Please wait before sending messages.");
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

        /// <summary>
        /// Deletes a chat message (moderators and channel owners only).
        /// Broadcasts MessageDeleted event to all viewers.
        /// </summary>
        public async Task DeleteMessage(int streamId, long messageId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "You must be authenticated.");
                return;
            }

            var stream = await _context.LiveStreams
                .FirstOrDefaultAsync(s => s.Id == streamId);

            if (stream == null)
            {
                await Clients.Caller.SendAsync("Error", "Stream not found.");
                return;
            }

            // Check moderation privileges
            if (!await _moderationService.HasModerationPrivilegesAsync(stream.ChannelId, userId))
            {
                await Clients.Caller.SendAsync("Error", "You do not have permission to delete messages.");
                return;
            }

            try
            {
                await _moderationService.DeleteMessageAsync(stream.ChannelId, userId, messageId);

                // Broadcast deletion to all viewers
                var groupName = GetGroupName(streamId);
                await Clients.Group(groupName).SendAsync("MessageDeleted", messageId);
            }
            catch (InvalidOperationException ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();

            // Clean up rate limit entry
            if (!string.IsNullOrEmpty(userId))
            {
                _rateLimits.TryRemove(userId, out _);
            }

            // Clean up viewer tracking and broadcast updated count
            if (_connectionStreams.TryRemove(Context.ConnectionId, out var streamId))
            {
                _viewerTracker.RemoveViewer(streamId, Context.ConnectionId);

                var groupName = GetGroupName(streamId);
                var count = _viewerTracker.GetViewerCount(streamId);
                await Clients.Group(groupName).SendAsync("ViewerCountUpdate", streamId, count);
            }
            else
            {
                // Fallback: remove from all streams
                _viewerTracker.RemoveViewerFromAll(Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
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
