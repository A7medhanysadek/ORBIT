namespace OrbitBackend.DTOs.Channel
{
    public class ChannelModeratorDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime HiredAt { get; set; }
    }
}
