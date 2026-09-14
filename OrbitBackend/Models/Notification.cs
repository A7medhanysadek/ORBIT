namespace OrbitBackend.Models
{
    /// <summary>
    /// Stores user notifications (live stream alerts, new followers, moderator hiring, etc.).
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Notification category: STREAM_LIVE, NEW_FOLLOWER, MOD_HIRED, SYSTEM.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optional JSON or string reference data (e.g., channelId, streamId, username).
        /// </summary>
        public string? Data { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ──
        public AppUser User { get; set; } = null!;
    }
}
