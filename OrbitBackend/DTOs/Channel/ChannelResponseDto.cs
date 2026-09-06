namespace OrbitBackend.DTOs.Channel
{
    public class ChannelResponseDto
    {
        public int Id { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsLive { get; set; }
        public int ModeratorCount { get; set; }
    }
}
