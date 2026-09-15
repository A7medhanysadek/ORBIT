using OrbitBackend.DTOs.Admin;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.DTOs.Common;
using OrbitBackend.DTOs.Vod;

namespace OrbitBackend.Services.Interfaces
{
    public interface IAdminService
    {
        // Stats
        Task<AdminStatsDto> GetSystemStatsAsync();

        // Users
        Task<PaginatedResponseDto<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search = null, string? role = null);
        Task<AdminUserDto> GetUserByIdAsync(string userId);
        Task UpdateUserRolesAsync(string currentAdminId, string targetUserId, UpdateUserRolesDto dto);
        Task LockUserAsync(string currentAdminId, string targetUserId, LockUserDto dto);
        Task ResetUserPasswordAsync(string currentAdminId, string targetUserId, AdminResetPasswordDto dto);
        Task DeleteUserAsync(string currentAdminId, string targetUserId);

        // Channels
        Task<PaginatedResponseDto<AdminChannelDto>> GetChannelsAsync(int page, int pageSize, string? search = null);
        Task<string> ResetChannelStreamKeyAsync(int channelId);
        Task DeleteChannelAsync(int channelId);

        // Live Streams
        Task<List<AdminStreamDto>> GetLiveStreamsAsync();
        Task ForceEndStreamAsync(int streamId);
        Task<AdminStreamDto> SimulateYoutubeStreamAsync(SimulateYoutubeStreamDto dto);
        Task EndSimulatedStreamAsync(int streamId);

        // Content
        Task<PaginatedResponseDto<ClipResponseDto>> GetClipsAsync(int page, int pageSize);
        Task DeleteClipAsync(int clipId);
        Task<PaginatedResponseDto<SavedLiveDto>> GetVodsAsync(int page, int pageSize);
        Task DeleteVodAsync(int vodId);
    }
}
