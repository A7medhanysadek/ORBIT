using OrbitBackend.DTOs.Dashboard;
using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    public interface IStreamService
    {
        Task<StreamKeyResponseDto> GenerateStreamKeyAsync(string userId);

        Task<StreamKeyResponseDto> GetStreamKeyAsync(string userId);

        Task<StreamResponseDto> CreateStreamAsync(string userId, CreateStreamDto dto);

        /// <summary>
        /// Updates category, title, or description of the streamer's active or pending stream.
        /// Broadcasts real-time SignalR notification to viewers.
        /// </summary>
        Task<StreamResponseDto> UpdateStreamAsync(string userId, UpdateLiveStreamDto dto);

        Task<bool> ValidateStreamKeyAsync(string streamKey);

        Task<MarkLiveResultDto?> MarkStreamLiveAsync(string streamKey);

        Task MarkStreamOfflineAsync(string streamKey);

        /// <summary>
        /// Manually ends the active or pending stream, notifies viewers, and cleans up tracking.
        /// Returns a session recap.
        /// </summary>
        Task<StreamSessionSummaryDto> EndStreamAsync(string userId);

        Task<List<LiveStreamSummaryDto>> GetLiveStreamsAsync();

        Task<StreamResponseDto> GetStreamByIdAsync(int id);

        /// <summary>
        /// Saves the recording file path received from nginx on_record_done callback.
        /// </summary>
        Task SaveRecordingPathAsync(string streamKey, string filePath);
    }

    public class MarkLiveResultDto
    {
        public int StreamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
}
