using OrbitBackend.DTOs.Vod;

namespace OrbitBackend.Services.Interfaces
{
    public interface IVodService
    {
        /// <summary>
        /// Lists all saved VODs for a channel (ended streams with recordings where SaveStreams is enabled).
        /// Includes rewatch count and chat message count for each VOD.
        /// </summary>
        Task<List<SavedLiveDto>> GetChannelVodsAsync(int channelId);

        /// <summary>
        /// Gets a single VOD with full chat history and rewatch count.
        /// </summary>
        Task<VodDetailDto> GetVodWithChatAsync(int vodId);

        /// <summary>
        /// Records a view on a VOD. Deduplicates by userId (authenticated) or sessionId (anonymous).
        /// </summary>
        Task RecordVodViewAsync(int vodId, string? userId, string? sessionId);

        /// <summary>
        /// Deletes a saved VOD (only by the channel owner or admin).
        /// </summary>
        Task DeleteVodAsync(int vodId, string userId);
    }
}
