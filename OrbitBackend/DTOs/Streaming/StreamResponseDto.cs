namespace OrbitBackend.DTOs.Streaming
{
    public class StreamResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsLive { get; set; }
        public string? HlsUrl { get; set; }

        /// <summary>
        /// URL to the recorded stream video for VOD playback. 
        /// Only populated for ended streams that have a recording.
        /// </summary>
        public string? VodUrl { get; set; }

        public string StreamerId { get; set; } = string.Empty;
        public string StreamerName { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
