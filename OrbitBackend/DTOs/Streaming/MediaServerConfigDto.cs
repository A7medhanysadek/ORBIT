namespace OrbitBackend.DTOs.Streaming
{
    public class MediaServerConfigDto
    {
        public bool IsConfigured { get; set; }
        public string? RtmpUrl { get; set; }
        public string? HlsBaseUrl { get; set; }
        public string? Message { get; set; }
    }
}
