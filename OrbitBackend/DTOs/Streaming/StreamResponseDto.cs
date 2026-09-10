namespace OrbitBackend.DTOs.Streaming
{
    public class StreamResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsLive { get; set; }
        public string? HlsUrl { get; set; }
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// URL to the recorded stream video for VOD playback. 
        /// Only populated for ended streams that have a recording.
        /// </summary>
        public string? VodUrl { get; set; }

        /// <summary>
        /// Set when the RTMP connection drops. Null when connected or ended.
        /// Frontend can show "Streamer reconnecting..." when this is set but IsLive is still true.
        /// </summary>
        public DateTime? DisconnectedAt { get; set; }

        /// <summary>
        /// Number of viewers currently watching this stream in real-time.
        /// </summary>
        public int ViewerCount { get; set; }

        public string StreamerId { get; set; } = string.Empty;
        public string StreamerName { get; set; } = string.Empty;
        public string? ChannelName { get; set; }
        public int ChannelId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // ── Category ──
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
    }
}
