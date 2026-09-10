namespace OrbitBackend.Models
{
    /// <summary>
    /// Tracks individual VOD (Video On Demand) views for rewatch counting.
    /// Supports both authenticated and anonymous viewers.
    /// </summary>
    public class VodView
    {
        public long Id { get; set; }

        public int LiveStreamId { get; set; }

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
        public LiveStream LiveStream { get; set; } = null!;
        public AppUser? User { get; set; }
    }
}
