namespace OrbitBackend.DTOs.Streaming
{
    public class StreamKeyResponseDto
    {
        public string StreamKey { get; set; } = string.Empty;
        public string RtmpUrl { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
