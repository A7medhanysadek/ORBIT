namespace OrbitBackend.Models
{
    /// <summary>
    /// Represents a social media link on a channel's profile page.
    /// Each channel can have multiple social links (Twitter, YouTube, Discord, etc.).
    /// </summary>
    public class ChannelSocialLink
    {
        public int Id { get; set; }

        public int ChannelId { get; set; }

        /// <summary>
        /// Platform identifier (e.g., "twitter", "youtube", "discord", "instagram", "tiktok").
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Full URL to the social media profile.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        // ── Navigation ──
        public Channel Channel { get; set; } = null!;
    }
}
