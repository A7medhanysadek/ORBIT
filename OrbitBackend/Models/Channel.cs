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
        /// Channel avatar / profile photo URL (Cloudinary).
        /// </summary>
        public string? ProfilePhotoUrl { get; set; }

        /// <summary>
        /// Channel banner / cover photo URL (Cloudinary).
        /// </summary>
        public string? CoverPhotoUrl { get; set; }

        /// <summary>
        /// External donation link (PayPal, Ko-fi, etc.).
        /// </summary>
        public string? DonationUrl { get; set; }

        /// <summary>
        /// Custom message displayed in the donation panel.
        /// </summary>
        public string? DonationMessage { get; set; }

        /// <summary>
        /// Whether to archive ended streams as VODs.
        /// </summary>
        public bool SaveStreams { get; set; } = true;

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
        public ICollection<ChannelSocialLink> SocialLinks { get; set; } = new List<ChannelSocialLink>();
        public ICollection<Clip> Clips { get; set; } = new List<Clip>();
        public ICollection<ChannelEmoji> CustomEmojis { get; set; } = new List<ChannelEmoji>();
        public ICollection<ChannelFollow> Followers { get; set; } = new List<ChannelFollow>();
    }
}

