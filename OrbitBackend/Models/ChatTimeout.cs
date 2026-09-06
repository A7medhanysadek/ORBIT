namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a temporary chat timeout for a user in a specific channel.
    /// The user cannot send messages until ExpiresAt.
    /// </summary>
    public class ChatTimeout
    {
        public long Id { get; set; }

        /// <summary>
        /// The channel where the timeout applies.
        /// </summary>
        public int ChannelId { get; set; }

        /// <summary>
        /// The user who was timed out.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The moderator or streamer who issued the timeout.
        /// </summary>
        public string ModeratorId { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason for the timeout.
        /// </summary>
        public string? Reason { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }

        // ── Navigation properties ──
        public Channel Channel { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public AppUser Moderator { get; set; } = null!;
    }
}
