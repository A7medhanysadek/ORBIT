namespace OrbitBackend.Models
{
    public class LiveStream
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsLive { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Set when the RTMP connection drops (OBS crash, network issue).
        /// Cleared when the streamer reconnects within the grace period.
        /// If the grace period expires, the stream is ended automatically.
        /// </summary>
        public DateTime? DisconnectedAt { get; set; }

        /// <summary>
        /// File name of the recorded stream (set by nginx on_record_done callback).
        /// Null if no recording exists.
        /// </summary>
        public string? RecordingFileName { get; set; }

        /// <summary>
        /// Stream thumbnail image URL (Cloudinary).
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        // ── Foreign Keys ──
        public string StreamerId { get; set; } = string.Empty;
        public int ChannelId { get; set; }

        /// <summary>
        /// Optional category for this stream (e.g., "Gaming", "Just Chatting").
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Peak concurrent viewers achieved during this stream session.
        /// </summary>
        public int PeakViewers { get; set; } = 0;

        // ── Navigation Properties ──
        public AppUser Streamer { get; set; } = null!;
        public Channel Channel { get; set; } = null!;
        public Category? Category { get; set; }
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public ICollection<VodView> VodViews { get; set; } = new List<VodView>();
        public ICollection<Clip> Clips { get; set; } = new List<Clip>();
    }
}

