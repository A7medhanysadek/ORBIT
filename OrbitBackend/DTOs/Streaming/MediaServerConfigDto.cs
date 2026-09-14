namespace OrbitBackend.DTOs.Streaming
{
    public class MediaServerConfigDto
    {
        public bool IsConfigured { get; set; }
        public bool IsCustomConfigured { get; set; }
        public string? RtmpUrl { get; set; }
        public string? HlsBaseUrl { get; set; }
        public string? ClipsBaseUrl { get; set; }
        public string? RecordingsBaseUrl { get; set; }
        public string? EffectiveRtmpUrl { get; set; }
        public string? EffectiveHlsBaseUrl { get; set; }
        public string? Message { get; set; }
    }
}
