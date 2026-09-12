using OrbitBackend.DTOs.Dashboard;
using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    public interface IDashboardService
    {
        /// <summary>
        /// Gets the comprehensive streamer dashboard overview including channel profile,
        /// current live session (if active), and lifetime performance KPIs.
        /// </summary>
        Task<StreamerDashboardSummaryDto> GetDashboardSummaryAsync(string userId);

        /// <summary>
        /// Gets real-time controls, metrics, and ingest credentials for the streamer's active broadcast.
        /// </summary>
        Task<CurrentStreamStatusDto?> GetLiveManagerStatusAsync(string userId);

        /// <summary>
        /// Gets a paginated list of past streams with full analytics (duration, peak viewers, chat count, VOD info).
        /// </summary>
        Task<List<PastStreamDto>> GetPastStreamsAsync(string userId, int page = 1, int pageSize = 10);

        /// <summary>
        /// Gets a summary of channel moderators, active bans, and active timeouts.
        /// </summary>
        Task<DashboardModerationSummaryDto> GetModerationSummaryAsync(string userId);

        /// <summary>
        /// Ends the streamer's current live session and returns a recap.
        /// </summary>
        Task<StreamSessionSummaryDto> EndCurrentStreamAsync(string userId);

        /// <summary>
        /// Updates the current live stream's title, description, and/or category.
        /// </summary>
        Task<StreamResponseDto> UpdateCurrentStreamMetadataAsync(string userId, UpdateLiveStreamDto dto);

        /// <summary>
        /// Sets (replaces all) custom emojis for the streamer's channel chat.
        /// </summary>
        Task<List<CustomEmojiResponseDto>> SetCustomEmojisAsync(string userId, SetCustomEmojisDto dto);

        /// <summary>
        /// Gets all custom emojis for the streamer's channel.
        /// </summary>
        Task<List<CustomEmojiResponseDto>> GetCustomEmojisAsync(string userId);

        /// <summary>
        /// Gets the platform badge emoji configuration (owner, moderator, OG badges).
        /// </summary>
        BadgeEmojisResponseDto GetBadgeEmojis();
    }
}
