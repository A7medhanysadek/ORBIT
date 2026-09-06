namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a streamer's channel. Users create a channel to become streamers.
    /// The stream key is tied to the channel, not the user directly.
    /// </summary>
    public class Channel
    {
        public int Id { get; set; }

        /// <summary>
        /// Unique display name for the channel (e.g., "AhmedPlays").
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// Optional description/bio for the channel.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// RTMP stream key used by OBS/streaming software.
        /// Generated via the API, unique per channel.
        /// </summary>
        public string? StreamKey { get; set; }

        /// <summary>
        /// Foreign key to the user who owns this channel.
        /// One user can have at most one channel.
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation properties ──
        public AppUser Owner { get; set; } = null!;
        public ICollection<LiveStream> LiveStreams { get; set; } = new List<LiveStream>();
        public ICollection<ChannelModerator> Moderators { get; set; } = new List<ChannelModerator>();
    }
}
