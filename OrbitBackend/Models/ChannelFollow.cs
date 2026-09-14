namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a user following a channel.
    /// Each user can follow a channel at most once.
    /// </summary>
    public class ChannelFollow
    {
        public long Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int ChannelId { get; set; }

        public DateTime FollowedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ──
        public AppUser User { get; set; } = null!;
        public Channel Channel { get; set; } = null!;
    }
}
