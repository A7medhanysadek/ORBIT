namespace OrbitBackend.Models
{
    /// <summary>
    /// Tracks individual clip views for per-user deduplication.
    /// Each user (or anonymous session) can only count as one view per clip.
    /// </summary>
    public class ClipView
    {
        public long Id { get; set; }

        public int ClipId { get; set; }

        /// <summary>
        /// User ID of the viewer. Null for anonymous viewers.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Session identifier for anonymous viewer deduplication.
        /// </summary>
        public string? SessionId { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ──
        public Clip Clip { get; set; } = null!;
        public AppUser? User { get; set; }
    }
}
