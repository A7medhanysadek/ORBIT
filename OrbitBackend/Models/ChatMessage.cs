namespace OrbitBackend.Models
{
    public class ChatMessage
    {
        public long Id { get; set; }

        /// <summary>
        /// Foreign key to the live stream this message belongs to.
        /// </summary>
        public int LiveStreamId { get; set; }

        /// <summary>
        /// Foreign key to the user who sent this message.
        /// </summary>
        public string SenderId { get; set; } = string.Empty;

        /// <summary>
        /// Denormalized sender display name for fast reads / broadcast.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// The chat message text content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Absolute UTC timestamp when the message was sent.
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Seconds elapsed since the stream's StartedAt.
        /// Used to synchronize chat replay with VOD playback position.
        /// </summary>
        public double StreamOffsetSeconds { get; set; }

        /// <summary>
        /// Soft-delete flag. Messages deleted by moderators are hidden but not removed.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// The user ID of the moderator/streamer who deleted this message.
        /// Null if the message has not been deleted.
        /// </summary>
        public string? DeletedByUserId { get; set; }

        // ── Navigation properties ──
        public LiveStream LiveStream { get; set; } = null!;
        public AppUser Sender { get; set; } = null!;
    }
}
