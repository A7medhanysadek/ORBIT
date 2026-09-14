using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    
    
    
    
    public interface IMediaServerConfigService
    {
        
        
        
        MediaServerConfigDto SetUrls(string rtmpUrl, string hlsBaseUrl, string? clipsBaseUrl = null, string? recordingsBaseUrl = null);

        
        
        
        MediaServerConfigDto ClearUrls();

        
        
        
        MediaServerConfigDto GetConfig();

        
        
        
        string GetRtmpUrl();

        string GetHlsBaseUrl();

        string GetControlUrl();

        string GetClipServiceUrl();

        string GetClipsBaseUrl();

        string GetRecordingsBaseUrl();

        bool IsConfigured { get; }
    }
}
