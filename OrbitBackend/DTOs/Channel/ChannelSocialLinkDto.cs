namespace OrbitBackend.DTOs.Channel
{
    public class ChannelSocialLinkDto
    {
        /// <summary>
        /// Platform identifier (e.g., "twitter", "youtube", "discord", "instagram", "tiktok").
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Full URL to the social media profile.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }
}
