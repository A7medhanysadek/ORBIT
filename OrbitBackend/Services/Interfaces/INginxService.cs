using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    
    
    
    
    public interface IMediaServerConfigService
    {
        
        
        
        MediaServerConfigDto SetUrls(string rtmpUrl, string hlsBaseUrl);

        
        
        
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
