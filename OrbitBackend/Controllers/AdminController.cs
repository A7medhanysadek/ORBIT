using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Admin;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.DTOs.Common;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.DTOs.Vod;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Full Platform System Administration API.
    /// Accessible only by users in the Admin role.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IMediaServerConfigService _mediaServerConfig;

        public AdminController(IAdminService adminService, IMediaServerConfigService mediaServerConfig)
        {
            _adminService = adminService;
            _mediaServerConfig = mediaServerConfig;
        }

        // ── System Stats ──

        [HttpGet("stats")]
        [ProducesResponseType(typeof(AdminStatsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _adminService.GetSystemStatsAsync();
            return Ok(stats);
        }

        // ── User Management ──

        [HttpGet("users")]
        [ProducesResponseType(typeof(PaginatedResponseDto<AdminUserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null)
        {
            var users = await _adminService.GetUsersAsync(page, pageSize, search, role);
            return Ok(users);
        }

        [HttpGet("users/{userId}")]
        [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(string userId)
        {
            var user = await _adminService.GetUserByIdAsync(userId);
            return Ok(user);
        }

        [HttpPut("users/{userId}/roles")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateUserRoles(string userId, [FromBody] UpdateUserRolesDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _adminService.UpdateUserRolesAsync(currentUserId, userId, dto);
            return Ok(new { message = "User roles updated successfully." });
        }

        [HttpPost("users/{userId}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LockUser(string userId, [FromBody] LockUserDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _adminService.LockUserAsync(currentUserId, userId, dto);
            var status = dto.IsLocked ? "locked" : "unlocked";
            return Ok(new { message = $"User account {status} successfully." });
        }

        [HttpPost("users/{userId}/reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetUserPassword(string userId, [FromBody] AdminResetPasswordDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _adminService.ResetUserPasswordAsync(currentUserId, userId, dto);
            return Ok(new { message = "User password has been reset." });
        }

        [HttpDelete("users/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var currentUserId = GetCurrentUserId();
            await _adminService.DeleteUserAsync(currentUserId, userId);
            return Ok(new { message = "User deleted successfully." });
        }

        // ── Channel Management ──

        [HttpGet("channels")]
        [ProducesResponseType(typeof(PaginatedResponseDto<AdminChannelDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChannels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            [FromQuery] string? search = null)
        {
            var channels = await _adminService.GetChannelsAsync(page, pageSize, search);
            return Ok(channels);
        }

        [HttpPost("channels/{channelId:int}/reset-stream-key")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetStreamKey(int channelId)
        {
            var newKey = await _adminService.ResetChannelStreamKeyAsync(channelId);
            return Ok(new { message = "Stream key regenerated successfully.", streamKey = newKey });
        }

        [HttpDelete("channels/{channelId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteChannel(int channelId)
        {
            await _adminService.DeleteChannelAsync(channelId);
            return Ok(new { message = "Channel deleted successfully." });
        }

        // ── Live Stream Moderation ──

        [HttpGet("streams/live")]
        [ProducesResponseType(typeof(List<AdminStreamDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLiveStreams()
        {
            var streams = await _adminService.GetLiveStreamsAsync();
            return Ok(streams);
        }

        [HttpPost("streams/{streamId:int}/force-end")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ForceEndStream(int streamId)
        {
            await _adminService.ForceEndStreamAsync(streamId);
            return Ok(new { message = "Live stream force-terminated successfully." });
        }

        [HttpPost("simulate-youtube-stream")]
        [ProducesResponseType(typeof(AdminStreamDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SimulateYoutubeStream([FromBody] SimulateYoutubeStreamDto dto)
        {
            var result = await _adminService.SimulateYoutubeStreamAsync(dto);
            return Ok(result);
        }

        [HttpPost("streams/{streamId:int}/end-simulated")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EndSimulatedStream(int streamId)
        {
            await _adminService.EndSimulatedStreamAsync(streamId);
            return Ok(new { message = "Simulated stream ended successfully." });
        }

        // ── Content Moderation (Clips & VODs) ──

        [HttpGet("clips")]
        [ProducesResponseType(typeof(PaginatedResponseDto<ClipResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClips([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var clips = await _adminService.GetClipsAsync(page, pageSize);
            return Ok(clips);
        }

        [HttpDelete("clips/{clipId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteClip(int clipId)
        {
            await _adminService.DeleteClipAsync(clipId);
            return Ok(new { message = "Clip deleted successfully." });
        }

        [HttpGet("vods")]
        [ProducesResponseType(typeof(PaginatedResponseDto<SavedLiveDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVods([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var vods = await _adminService.GetVodsAsync(page, pageSize);
            return Ok(vods);
        }

        [HttpDelete("vods/{vodId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVod(int vodId)
        {
            await _adminService.DeleteVodAsync(vodId);
            return Ok(new { message = "VOD deleted successfully." });
        }

        // ── Media Server Configuration ──

        [HttpGet("media-server/config")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        public IActionResult GetMediaServerConfig()
        {
            var result = _mediaServerConfig.GetConfig();
            return Ok(result);
        }

        [HttpPost("media-server/set-url")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public IActionResult SetMediaServerUrl([FromBody] SetMediaServerUrlDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RtmpUrl) || string.IsNullOrWhiteSpace(dto.HlsBaseUrl))
                return BadRequest(new { message = "Both RTMP and HLS URLs are required." });

            var result = _mediaServerConfig.SetUrls(dto.RtmpUrl, dto.HlsBaseUrl, dto.ClipsBaseUrl, dto.RecordingsBaseUrl);
            return Ok(result);
        }

        [HttpPost("media-server/clear-url")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        public IActionResult ClearMediaServerUrl()
        {
            var result = _mediaServerConfig.ClearUrls();
            return Ok(result);
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Could not identify the user from the token.");

            return userId;
        }
    }
}
