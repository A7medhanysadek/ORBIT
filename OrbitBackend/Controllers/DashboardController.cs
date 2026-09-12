using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Dashboard;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Streamer Dashboard / Creator Studio API.
    /// Provides analytics, live stream management, past broadcast history,
    /// community moderation overview, and channel configuration.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Streamer")]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IVodService _vodService;

        public DashboardController(IDashboardService dashboardService, IVodService vodService)
        {
            _dashboardService = dashboardService;
            _vodService = vodService;
        }

        /// <summary>
        /// Gets the comprehensive streamer dashboard summary (Channel profile, active live manager, and lifetime KPI stats).
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(StreamerDashboardSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var userId = GetUserId();
            var result = await _dashboardService.GetDashboardSummaryAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Gets real-time controls, metrics, and stream credentials for the streamer's active broadcast.
        /// Returns 204 No Content if the streamer is offline.
        /// </summary>
        [HttpGet("live-manager")]
        [ProducesResponseType(typeof(CurrentStreamStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetLiveManagerStatus()
        {
            var userId = GetUserId();
            var result = await _dashboardService.GetLiveManagerStatusAsync(userId);
            if (result == null)
                return NoContent();

            return Ok(result);
        }

        /// <summary>
        /// Updates the current live stream's title, description, or category while live.
        /// Broadcasts real-time SignalR event to all watching viewers.
        /// </summary>
        [HttpPatch("stream/current")]
        [ProducesResponseType(typeof(StreamResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateCurrentStreamMetadata([FromBody] UpdateLiveStreamDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _dashboardService.UpdateCurrentStreamMetadataAsync(userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Ends the current broadcast session, disconnects RTMP in NGINX, and returns a session recap.
        /// </summary>
        [HttpPost("stream/end")]
        [ProducesResponseType(typeof(StreamSessionSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> EndCurrentStream()
        {
            var userId = GetUserId();
            var summary = await _dashboardService.EndCurrentStreamAsync(userId);
            return Ok(summary);
        }

        /// <summary>
        /// Gets a paginated list of past streams with full analytics (duration, peak viewers, VOD views, chat count, clips count).
        /// </summary>
        [HttpGet("streams")]
        [ProducesResponseType(typeof(List<PastStreamDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPastStreams([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetUserId();
            var result = await _dashboardService.GetPastStreamsAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Deletes a saved VOD from the channel archives.
        /// </summary>
        [HttpDelete("vods/{vodId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteVod(int vodId)
        {
            var userId = GetUserId();
            await _vodService.DeleteVodAsync(vodId, userId);
            return Ok(new { message = "VOD deleted successfully." });
        }

        /// <summary>
        /// Gets an overview of channel moderators, active bans, and active timeouts.
        /// </summary>
        [HttpGet("moderation")]
        [ProducesResponseType(typeof(DashboardModerationSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetModerationSummary()
        {
            var userId = GetUserId();
            var result = await _dashboardService.GetModerationSummaryAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Sets (replaces all) custom emojis for the streamer's channel chat.
        /// Maximum 50 custom emojis per channel.
        /// </summary>
        [HttpPut("emojis/custom")]
        [ProducesResponseType(typeof(List<CustomEmojiResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetCustomEmojis([FromBody] SetCustomEmojisDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _dashboardService.SetCustomEmojisAsync(userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Gets all custom emojis for the streamer's channel.
        /// </summary>
        [HttpGet("emojis/custom")]
        [ProducesResponseType(typeof(List<CustomEmojiResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCustomEmojis()
        {
            var userId = GetUserId();
            var result = await _dashboardService.GetCustomEmojisAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Gets the platform badge emoji configuration (owner 🌍, moderator 🪐, OG ⭐).
        /// </summary>
        [HttpGet("emojis/badges")]
        [ProducesResponseType(typeof(BadgeEmojisResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetBadgeEmojis()
        {
            var result = _dashboardService.GetBadgeEmojis();
            return Ok(result);
        }

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Could not identify the user from the token.");

            return userId;
        }
    }
}
