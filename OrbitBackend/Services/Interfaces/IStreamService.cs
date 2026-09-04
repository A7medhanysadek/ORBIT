using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    public interface IStreamService
    {
        
        
        
        Task<StreamKeyResponseDto> GenerateStreamKeyAsync(string userId);

        
        
        
        Task<StreamKeyResponseDto> GetStreamKeyAsync(string userId);

        
        
        
        Task<StreamResponseDto> CreateStreamAsync(string userId, CreateStreamDto dto);

        
        
        
        
        Task<bool> ValidateStreamKeyAsync(string streamKey);

        
        
        
        Task MarkStreamLiveAsync(string streamKey);

        
        
        
        Task MarkStreamOfflineAsync(string streamKey);

        
        
        
        Task EndStreamAsync(string userId);

        
        
        
        Task<List<LiveStreamSummaryDto>> GetLiveStreamsAsync();

        
        
        
        Task<StreamResponseDto> GetStreamByIdAsync(int id);

        /// <summary>
        /// Saves the recording file path received from nginx on_record_done callback.
        /// </summary>
        Task SaveRecordingPathAsync(string streamKey, string filePath);
    }
}
