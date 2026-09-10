namespace OrbitBackend.DTOs.Channel
{
    public class ChannelResponseDto
    {
        public int Id { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsLive { get; set; }
        public int ModeratorCount { get; set; }

        // ── Channel customization ──
        public string? ProfilePhotoUrl { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public string? DonationUrl { get; set; }
        public string? DonationMessage { get; set; }
        public bool SaveStreams { get; set; }
        public List<ChannelSocialLinkDto> SocialLinks { get; set; } = new();
    }
}
