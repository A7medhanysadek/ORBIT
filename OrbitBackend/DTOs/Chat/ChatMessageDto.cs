namespace OrbitBackend.DTOs.Chat
{
    public class ChatMessageDto
    {
        public long Id { get; set; }
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Role badge emoji: 🌍 = channel owner, 🪐 = moderator, ⭐ = OG user, null = regular.
        /// </summary>
        public string? SenderBadge { get; set; }

        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Seconds since stream start — used by VOD player to
        /// display chat messages at the correct playback time.
        /// </summary>
        public double StreamOffsetSeconds { get; set; }
    }
}
