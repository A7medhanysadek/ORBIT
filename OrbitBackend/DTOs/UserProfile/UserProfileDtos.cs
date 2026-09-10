namespace OrbitBackend.DTOs.UserProfile
{
    public class UpdateProfilePictureResponseDto
    {
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class UserPublicProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }

        /// <summary>
        /// Channel info (null if user doesn't have a channel).
        /// </summary>
        public int? ChannelId { get; set; }
        public string? ChannelName { get; set; }
    }
}
