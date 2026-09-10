using OrbitBackend.DTOs.Chat;

namespace OrbitBackend.DTOs.Vod
{
    /// <summary>
    /// Summary DTO for a saved live stream (VOD) in a channel's archive listing.
    /// </summary>
    public class SavedLiveDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VodUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }

        /// <summary>
        /// Duration of the stream in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Total number of unique rewatch views for this VOD.
        /// </summary>
        public int RewatchCount { get; set; }

        /// <summary>
        /// Total number of saved chat messages in this VOD.
        /// </summary>
        public int ChatMessageCount { get; set; }
    }

    /// <summary>
    /// Detailed DTO for a single VOD with its full chat history.
    /// </summary>
    public class VodDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VodUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public double? DurationSeconds { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int RewatchCount { get; set; }

        // ── Streamer / Channel Info ──
        public string StreamerName { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public int ChannelId { get; set; }

        /// <summary>
        /// All non-deleted chat messages for this VOD, ordered by stream offset.
        /// </summary>
        public List<ChatMessageDto> ChatMessages { get; set; } = new();
    }
}
