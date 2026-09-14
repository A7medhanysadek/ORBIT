namespace OrbitBackend.DTOs.Channel
{
    /// <summary>
    /// DTO for a followed channel in the user's following list.
    /// </summary>
    public class ChannelFollowDto
    {
        public int ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string OwnerUsername { get; set; } = string.Empty;
        public string? ProfilePhotoUrl { get; set; }
        public bool IsLive { get; set; }
        public DateTime FollowedAt { get; set; }
    }
}
