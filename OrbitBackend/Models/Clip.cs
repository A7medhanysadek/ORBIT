namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a short highlight video clip created from a live stream, VOD, or uploaded to a channel.
    /// </summary>
    public class Clip
    {
        public int Id { get; set; }

        /// <summary>
        /// Title / headline of the clip (e.g. "Insane 1v4 clutch!").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Direct URL to the clip video file (Cloudinary video URL).
        /// </summary>
        public string VideoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Thumbnail image URL for the clip preview.
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Duration of the clip in seconds (typically 10-60 seconds).
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Total number of times this clip has been viewed across the platform.
        /// </summary>
        public int ViewCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Foreign Keys ──

        /// <summary>
        /// User who created/clipped this moment.
        /// </summary>
        public string CreatorId { get; set; } = string.Empty;

        /// <summary>
        /// Channel this clip belongs to.
        /// </summary>
        public int ChannelId { get; set; }

        /// <summary>
        /// The live stream from which this clip was taken (optional).
        /// </summary>
        public int? LiveStreamId { get; set; }

        /// <summary>
        /// Category for this clip (inherited from the stream or assigned).
        /// </summary>
        public int? CategoryId { get; set; }

        // ── Navigation Properties ──
        public AppUser Creator { get; set; } = null!;
        public Channel Channel { get; set; } = null!;
        public LiveStream? LiveStream { get; set; }
        public Category? Category { get; set; }
        public ICollection<ClipView> Views { get; set; } = new List<ClipView>();
    }
}
