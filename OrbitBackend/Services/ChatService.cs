using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Chat;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ILogger<ChatService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<ChatMessageDto> SaveMessageAsync(int streamId, string senderId, string content)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Channel)
                .FirstOrDefaultAsync(s => s.Id == streamId && s.IsLive)
                ?? throw new InvalidOperationException("Stream not found or is not live.");

            var user = await _userManager.FindByIdAsync(senderId)
                ?? throw new InvalidOperationException("User not found.");

            // Resolve role-based badge emoji (priority: owner > moderator > OG)
            string? badge = await ResolveSenderBadgeAsync(stream.ChannelId, stream.Channel.OwnerId, senderId, user.IsOgUser);

            // Calculate offset from stream start for VOD replay synchronization
            var offsetSeconds = stream.StartedAt.HasValue
                ? (DateTime.UtcNow - stream.StartedAt.Value).TotalSeconds
                : 0;

            var message = new ChatMessage
            {
                LiveStreamId = streamId,
                SenderId = senderId,
                SenderName = user.FullName,
                SenderBadge = badge,
                Content = content,
                SentAt = DateTime.UtcNow,
                StreamOffsetSeconds = Math.Max(0, offsetSeconds)
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Chat message {MessageId} saved for stream {StreamId} at offset {Offset}s.",
                message.Id, streamId, message.StreamOffsetSeconds);

            return MapToDto(message, user);
        }

        public async Task<List<ChatMessageDto>> GetStreamChatAsync(int streamId)
        {
            return await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.LiveStreamId == streamId && !m.IsDeleted)
                .OrderBy(m => m.StreamOffsetSeconds)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderName = m.SenderName,
                    SenderUsername = m.Sender != null ? m.Sender.UserName : null,
                    SenderId = m.SenderId,
                    SenderAvatarUrl = m.Sender != null ? m.Sender.ProfilePictureUrl : null,
                    SenderBadge = m.SenderBadge,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    StreamOffsetSeconds = m.StreamOffsetSeconds
                })
                .ToListAsync();
        }

        public async Task<List<ChatMessageDto>> GetStreamChatRangeAsync(int streamId, double fromSeconds, double toSeconds)
        {
            return await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.LiveStreamId == streamId
                         && !m.IsDeleted
                         && m.StreamOffsetSeconds >= fromSeconds
                         && m.StreamOffsetSeconds <= toSeconds)
                .OrderBy(m => m.StreamOffsetSeconds)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderName = m.SenderName,
                    SenderUsername = m.Sender != null ? m.Sender.UserName : null,
                    SenderId = m.SenderId,
                    SenderAvatarUrl = m.Sender != null ? m.Sender.ProfilePictureUrl : null,
                    SenderBadge = m.SenderBadge,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    StreamOffsetSeconds = m.StreamOffsetSeconds
                })
                .ToListAsync();
        }

        private static ChatMessageDto MapToDto(ChatMessage message, AppUser? sender = null)
        {
            return new ChatMessageDto
            {
                Id = message.Id,
                SenderName = message.SenderName,
                SenderUsername = sender?.UserName ?? message.Sender?.UserName,
                SenderId = message.SenderId,
                SenderAvatarUrl = sender?.ProfilePictureUrl ?? message.Sender?.ProfilePictureUrl,
                SenderBadge = message.SenderBadge,
                Content = message.Content,
                SentAt = message.SentAt,
                StreamOffsetSeconds = message.StreamOffsetSeconds
            };
        }

        /// <summary>
        /// Resolves the sender's role badge emoji for this channel.
        /// Priority: 🌍 channel owner > 🪐 moderator > ⭐ OG user > null regular.
        /// </summary>
        private async Task<string?> ResolveSenderBadgeAsync(int channelId, string channelOwnerId, string senderId, bool isOgUser)
        {
            // Channel owner gets the Earth badge (highest priority)
            if (senderId == channelOwnerId)
                return "🌍";

            // Check if sender is a moderator for this channel
            var isModerator = await _context.ChannelModerators
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == senderId);

            if (isModerator)
                return "🪐";

            // OG users (first 100 registered) get the star badge
            if (isOgUser)
                return "⭐";

            return null;
        }
    }
}
