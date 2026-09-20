using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Dashboard
{
    // ── Custom Channel Emojis ──

    /// <summary>
    /// DTO for adding/updating a custom emoji on the channel.
    /// </summary>
    public class CustomEmojiDto
    {
        /// <summary>
        /// Short name/code (used as :name: in chat). Must be alphanumeric + underscores.
        /// </summary>
        [Required(ErrorMessage = "Emoji name is required.")]
        [StringLength(32, MinimumLength = 2, ErrorMessage = "Emoji name must be between 2 and 32 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Emoji name can only contain letters, numbers, and underscores.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The emoji character (Unicode) or URL / base64 SVG/PNG data for a custom image.
        /// </summary>
        [Required(ErrorMessage = "Emoji value is required.")]
        [MaxLength(2000000, ErrorMessage = "Emoji value is too large.")]
        public string EmojiValue { get; set; } = string.Empty;

        /// <summary>
        /// True if EmojiValue is a URL to a custom image, false if it's a Unicode emoji.
        /// </summary>
        public bool IsCustomImage { get; set; }
    }

    /// <summary>
    /// Request DTO for setting (replace-all) the channel's custom emojis.
    /// </summary>
    public class SetCustomEmojisDto
    {
        /// <summary>
        /// The full list of custom emojis for this channel. Replaces all existing ones.
        /// Maximum 50 custom emojis per channel.
        /// </summary>
        [MaxLength(50, ErrorMessage = "Maximum 50 custom emojis per channel.")]
        public List<CustomEmojiDto> Emojis { get; set; } = new();
    }

    /// <summary>
    /// Response DTO for custom emoji.
    /// </summary>
    public class CustomEmojiResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmojiValue { get; set; } = string.Empty;
        public bool IsCustomImage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Badge Emojis ──

    /// <summary>
    /// Response DTO showing the role-based badge emoji configuration.
    /// </summary>
    public class BadgeEmojisResponseDto
    {
        /// <summary>
        /// Badge for the channel owner.
        /// </summary>
        public BadgeInfoDto Owner { get; set; } = new();

        /// <summary>
        /// Badge for channel moderators.
        /// </summary>
        public BadgeInfoDto Moderator { get; set; } = new();

        /// <summary>
        /// Badge for OG users (first 100 registered).
        /// </summary>
        public BadgeInfoDto OgUser { get; set; } = new();
    }

    public class BadgeInfoDto
    {
        public string Role { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
