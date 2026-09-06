namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a permanent chat ban for a user in a specific channel.
    /// The user cannot send messages until unbanned.
    /// </summary>
    public class ChatBan
    {
        public long Id { get; set; }

        /// <summary>
        /// The channel where the ban applies.
        /// </summary>
        public int ChannelId { get; set; }

        /// <summary>
        /// The user who was banned.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The moderator or streamer who issued the ban.
        /// </summary>
        public string ModeratorId { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason for the ban.
        /// </summary>
        public string? Reason { get; set; }

        public DateTime BannedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this ban is currently active. Set to false when unbanned.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ── Navigation properties ──
        public Channel Channel { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public AppUser Moderator { get; set; } = null!;
    }
}
