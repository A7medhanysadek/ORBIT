using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    
    
    
    
    
    public class MediaServerConfigService : IMediaServerConfigService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MediaServerConfigService> _logger;
        private readonly object _lock = new();
        private string? _rtmpUrl;
        private string? _hlsBaseUrl;

        public MediaServerConfigService(IConfiguration config, ILogger<MediaServerConfigService> logger)
        {
            _config = config;
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
                return _rtmpUrl ?? _config["Streaming:RtmpServerUrl"] ?? "rtmp://localhost:1935/live";
            }
        }

        public string GetHlsBaseUrl()
        {
            lock (_lock)
            {
                return _hlsBaseUrl ?? _config["Streaming:HlsBaseUrl"] ?? "http://localhost:8080/hls";
            }
        }

        public string GetControlUrl()
        {
            var configured = _config["Streaming:MediaServerControlUrl"];
            if (!string.IsNullOrEmpty(configured))
                return configured.TrimEnd('/');

            var hlsBase = GetHlsBaseUrl();
            return hlsBase.Replace("/hls", "/control");
        }

        public string GetClipServiceUrl()
        {
            var configured = _config["Streaming:MediaServerClipUrl"];
            if (!string.IsNullOrEmpty(configured))
                return configured.TrimEnd('/');

            var hlsBase = GetHlsBaseUrl();
            return hlsBase.Replace("/hls", "/api/clip");
        }

        public string GetClipsBaseUrl()
        {
            var configured = _config["Streaming:ClipsBaseUrl"];
            if (!string.IsNullOrEmpty(configured))
                return configured.TrimEnd('/');

            var hlsBase = GetHlsBaseUrl();
            return hlsBase.Replace("/hls", "/clips");
        }

        public string GetRecordingsBaseUrl()
        {
            var configured = _config["Streaming:RecordingsBaseUrl"];
            if (!string.IsNullOrEmpty(configured))
                return configured.TrimEnd('/');

            var hlsBase = GetHlsBaseUrl();
            return hlsBase.Replace("/hls", "/recordings");
        }

        private MediaServerConfigDto BuildConfigDto(string message)
        {
            lock (_lock)
            {
                var effectiveRtmp = GetRtmpUrl();
                var effectiveHls = GetHlsBaseUrl();
                var clipsBase = GetClipsBaseUrl();
                var recordingsBase = GetRecordingsBaseUrl();
                var isCustom = !string.IsNullOrEmpty(_rtmpUrl) && !string.IsNullOrEmpty(_hlsBaseUrl);

                return new MediaServerConfigDto
                {
                    IsConfigured = isCustom || !string.IsNullOrEmpty(effectiveHls),
                    IsCustomConfigured = isCustom,
                    RtmpUrl = _rtmpUrl ?? effectiveRtmp,
                    HlsBaseUrl = _hlsBaseUrl ?? effectiveHls,
                    ClipsBaseUrl = clipsBase,
                    RecordingsBaseUrl = recordingsBase,
                    EffectiveRtmpUrl = effectiveRtmp,
                    EffectiveHlsBaseUrl = effectiveHls,
                    Message = message
                };
            }
        }
    }
}
