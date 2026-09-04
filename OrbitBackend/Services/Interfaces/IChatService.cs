using OrbitBackend.DTOs.Chat;

namespace OrbitBackend.Services.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Persists a chat message and returns the DTO for broadcast.
        /// Calculates StreamOffsetSeconds automatically from the stream's StartedAt.
        /// </summary>
        Task<ChatMessageDto> SaveMessageAsync(int streamId, string senderId, string content);

        /// <summary>
        /// Gets all chat messages for a stream, ordered by offset.
        /// Used for full VOD chat replay.
        /// </summary>
        Task<List<ChatMessageDto>> GetStreamChatAsync(int streamId);

        /// <summary>
        /// Gets chat messages within a time window (seconds from stream start).
        /// Used for seeking in VOD — client requests messages around the current playback position.
        /// </summary>
        Task<List<ChatMessageDto>> GetStreamChatRangeAsync(int streamId, double fromSeconds, double toSeconds);
    }
}
