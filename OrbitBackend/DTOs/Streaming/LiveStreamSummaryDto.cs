namespace OrbitBackend.DTOs.Streaming
{
    public class LiveStreamSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string StreamerName { get; set; } = string.Empty;
        public string? ChannelName { get; set; }
        public int ChannelId { get; set; }
        public string? HlsUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime? StartedAt { get; set; }

        // ── Category ──
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }

        /// <summary>
        /// True when the streamer's RTMP connection dropped but the grace period hasn't expired yet.
        /// </summary>
        public bool IsReconnecting { get; set; }

        /// <summary>
        /// Number of viewers currently watching this stream in real-time.
        /// </summary>
        public int ViewerCount { get; set; }

        public string? YoutubeUrl { get; set; }
        public bool IsSimulated { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
