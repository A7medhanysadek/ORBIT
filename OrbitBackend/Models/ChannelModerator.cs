namespace OrbitBackend.Models
{
    /// <summary>
    /// Join entity linking a moderator (AppUser) to a Channel.
    /// Moderators are per-channel — a user can moderate multiple channels.
    /// </summary>
    public class ChannelModerator
    {
        public int ChannelId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime HiredAt { get; set; } = DateTime.UtcNow;

        // ── Navigation properties ──
        public Channel Channel { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
