using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Vod;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Endpoints for saved lives (VODs) — browsing channel archives,
    /// viewing VOD details with chat, and tracking rewatch views.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class VodController : ControllerBase
    {
        private readonly IVodService _vodService;

        public VodController(IVodService vodService)
        {
            _vodService = vodService;
        }

        /// <summary>
        /// Lists all saved VODs for a channel.
        /// Only shows ended streams with recordings where the channel has SaveStreams enabled.
        /// Each VOD includes its total rewatch count and chat message count.
        /// </summary>
        [HttpGet("channel/{channelId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<SavedLiveDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetChannelVods(int channelId)
        {
            var result = await _vodService.GetChannelVodsAsync(channelId);
            return Ok(result);
        }

        /// <summary>
        /// Gets a single VOD with its full saved chat history and rewatch count.
        /// Chat messages are ordered by stream offset for VOD replay synchronization.
        /// </summary>
        [HttpGet("{vodId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(VodDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVodWithChat(int vodId)
        {
            var result = await _vodService.GetVodWithChatAsync(vodId);
            return Ok(result);
        }

        /// <summary>
        /// Records a view on a VOD for rewatch counting.
        /// Authenticated users are tracked by user ID (deduplicated).
        /// Anonymous users can provide a sessionId query parameter for deduplication.
        /// </summary>
        [HttpPost("{vodId:int}/view")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecordVodView(int vodId, [FromQuery] string? sessionId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            await _vodService.RecordVodViewAsync(vodId, userId, sessionId);
            return Ok(new { message = "View recorded." });
        }

        /// <summary>
        /// Deletes a saved VOD from the channel archive. Only the channel owner can delete it.
        /// </summary>
        [HttpDelete("{vodId:int}")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteVod(int vodId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Could not identify the user from the token.");

            await _vodService.DeleteVodAsync(vodId, userId);
            return Ok(new { message = "VOD deleted successfully." });
        }
    }
}
