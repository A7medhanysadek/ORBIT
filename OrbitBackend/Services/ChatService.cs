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
                .FirstOrDefaultAsync(s => s.Id == streamId && s.IsLive)
                ?? throw new InvalidOperationException("Stream not found or is not live.");

            var user = await _userManager.FindByIdAsync(senderId)
                ?? throw new InvalidOperationException("User not found.");

            // Calculate offset from stream start for VOD replay synchronization
            var offsetSeconds = stream.StartedAt.HasValue
                ? (DateTime.UtcNow - stream.StartedAt.Value).TotalSeconds
                : 0;

            var message = new ChatMessage
            {
                LiveStreamId = streamId,
                SenderId = senderId,
                SenderName = user.FullName,
                Content = content,
                SentAt = DateTime.UtcNow,
                StreamOffsetSeconds = Math.Max(0, offsetSeconds)
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Chat message {MessageId} saved for stream {StreamId} at offset {Offset}s.",
                message.Id, streamId, message.StreamOffsetSeconds);

            return MapToDto(message);
        }

        public async Task<List<ChatMessageDto>> GetStreamChatAsync(int streamId)
        {
            return await _context.ChatMessages
                .Where(m => m.LiveStreamId == streamId && !m.IsDeleted)
                .OrderBy(m => m.StreamOffsetSeconds)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderName = m.SenderName,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    StreamOffsetSeconds = m.StreamOffsetSeconds
                })
                .ToListAsync();
        }

        public async Task<List<ChatMessageDto>> GetStreamChatRangeAsync(int streamId, double fromSeconds, double toSeconds)
        {
            return await _context.ChatMessages
                .Where(m => m.LiveStreamId == streamId
                         && !m.IsDeleted
                         && m.StreamOffsetSeconds >= fromSeconds
                         && m.StreamOffsetSeconds <= toSeconds)
                .OrderBy(m => m.StreamOffsetSeconds)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderName = m.SenderName,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    StreamOffsetSeconds = m.StreamOffsetSeconds
                })
                .ToListAsync();
        }

        private static ChatMessageDto MapToDto(ChatMessage message)
        {
            return new ChatMessageDto
            {
                Id = message.Id,
                SenderName = message.SenderName,
                Content = message.Content,
                SentAt = message.SentAt,
                StreamOffsetSeconds = message.StreamOffsetSeconds
            };
        }
    }
}
