using Microsoft.AspNetCore.Identity;

namespace OrbitBackend.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }

        /// <summary>
        /// URL to the user's profile picture (Cloudinary).
        /// </summary>
        public string? ProfilePictureUrl { get; set; }

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // ── Channel (one-to-one, nullable — only if user has created a channel) ──
        public Channel? Channel { get; set; }

        // ── Collections ──
        public ICollection<LiveStream> LiveStreams { get; set; } = new List<LiveStream>();
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public ICollection<ChannelModerator> ModeratorOf { get; set; } = new List<ChannelModerator>();
        public ICollection<Clip> CreatedClips { get; set; } = new List<Clip>();
    }
}
