using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    
    
    
    
    
    public class MediaServerConfigService : IMediaServerConfigService
    {
        private readonly ILogger<MediaServerConfigService> _logger;
        private readonly object _lock = new();
        private string? _rtmpUrl;
        private string? _hlsBaseUrl;

        public MediaServerConfigService(ILogger<MediaServerConfigService> logger)
        {
            _logger = logger;
        }

        public bool IsConfigured
        {
            get
            {
                lock (_lock)
                {
                    return !string.IsNullOrEmpty(_rtmpUrl) && !string.IsNullOrEmpty(_hlsBaseUrl);
                }
            }
        }

        public MediaServerConfigDto SetUrls(string rtmpUrl, string hlsBaseUrl)
        {
            
            rtmpUrl = rtmpUrl.TrimEnd('/');
            hlsBaseUrl = hlsBaseUrl.TrimEnd('/');

            lock (_lock)
            {
                _rtmpUrl = rtmpUrl;
                _hlsBaseUrl = hlsBaseUrl;
            }

            _logger.LogInformation(
                "Media server URLs configured — RTMP: {RtmpUrl}, HLS: {HlsBaseUrl}",
                rtmpUrl, hlsBaseUrl);

            return BuildConfigDto("Media server URLs configured successfully.");
        }

        public MediaServerConfigDto ClearUrls()
        {
            lock (_lock)
            {
                _rtmpUrl = null;
                _hlsBaseUrl = null;
            }

            _logger.LogInformation("Media server URLs cleared. Streaming is now offline.");
            return BuildConfigDto("Media server URLs cleared. Streaming is offline.");
        }

        public MediaServerConfigDto GetConfig()
        {
            return BuildConfigDto(
                IsConfigured
                    ? "Media server is configured and ready."
                    : "Media server URLs not configured. An admin must set the URLs first.");
        }

        public string GetRtmpUrl()
        {
            lock (_lock)
            {
                return _rtmpUrl ?? "rtmp://localhost:1935/live";
            }
        }

        public string GetHlsBaseUrl()
        {
            lock (_lock)
            {
                return _hlsBaseUrl ?? "http://localhost:8080/hls";
            }
        }

        private MediaServerConfigDto BuildConfigDto(string message)
        {
            lock (_lock)
            {
                return new MediaServerConfigDto
                {
                    IsConfigured = !string.IsNullOrEmpty(_rtmpUrl) && !string.IsNullOrEmpty(_hlsBaseUrl),
                    RtmpUrl = _rtmpUrl,
                    HlsBaseUrl = _hlsBaseUrl,
                    Message = message
                };
            }
        }
    }
}
