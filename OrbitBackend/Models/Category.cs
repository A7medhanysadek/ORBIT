namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a stream category (e.g., "Just Chatting", "Gaming", "Music").
    /// Streams are assigned to a category when created.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }

        /// <summary>
        /// Display name for the category (e.g., "Just Chatting").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL-friendly slug (e.g., "just-chatting"). Unique.
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Category box art / thumbnail image URL (Cloudinary).
        /// </summary>
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ──
        public ICollection<LiveStream> LiveStreams { get; set; } = new List<LiveStream>();
        public ICollection<Clip> Clips { get; set; } = new List<Clip>();
    }
}
