namespace OrbitBackend.DTOs.Streaming
{
    public class LiveStreamSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string StreamerName { get; set; } = string.Empty;
        public string? HlsUrl { get; set; }
        public DateTime? StartedAt { get; set; }
    }
}
