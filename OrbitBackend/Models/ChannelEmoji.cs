namespace OrbitBackend.Models
{
    /// <summary>
    /// Custom emoji added by a streamer to their channel's chat.
    /// Can be a Unicode emoji or a URL to a custom image.
    /// </summary>
    public class ChannelEmoji
    {
        public int Id { get; set; }

        /// <summary>
        /// Short name/code for the emoji (e.g., "hype", "gg", "sadge").
        /// Used as :name: in chat.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The emoji character (Unicode) or URL to a custom image.
        /// </summary>
        public string EmojiValue { get; set; } = string.Empty;

        /// <summary>
        /// True if EmojiValue is a URL to an image, false if it's a Unicode emoji.
        /// </summary>
        public bool IsCustomImage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Foreign keys ──
        public int ChannelId { get; set; }

        // ── Navigation ──
        public Channel Channel { get; set; } = null!;
    }
}
