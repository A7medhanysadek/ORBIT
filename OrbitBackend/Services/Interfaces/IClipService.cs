using OrbitBackend.DTOs.Clip;

namespace OrbitBackend.Services.Interfaces
{
    public interface IClipService
    {
        Task<ClipResponseDto> CreateClipAsync(string userId, CreateClipDto dto, IFormFile? videoFile);

        /// <summary>
        /// Slices a clip from a live stream on the media server side (default 60s, max 300s).
        /// </summary>
        Task<ClipResponseDto> SliceLiveClipAsync(string userId, SliceClipDto dto);

        Task<List<ClipResponseDto>> GetChannelClipsAsync(int channelId, int page = 1, int pageSize = 20);

        /// <summary>
        /// Gets the top watched clips across the entire platform, ordered by ViewCount descending.
        /// </summary>
        Task<List<ClipResponseDto>> GetTopClipsAsync(int count = 20);

        /// <summary>
        /// Gets the top watched clips within a specific category by category ID.
        /// </summary>
        Task<List<ClipResponseDto>> GetTopClipsByCategoryAsync(int categoryId, int count = 20);

        /// <summary>
        /// Gets the top watched clips within a specific category by category slug.
        /// </summary>
        Task<List<ClipResponseDto>> GetTopClipsByCategorySlugAsync(string slug, int count = 20);

        Task<ClipResponseDto> GetClipByIdAsync(int clipId);

        Task RecordClipViewAsync(int clipId, string? userId, string? sessionId);

        Task DeleteClipAsync(int clipId, string userId);
    }
}
