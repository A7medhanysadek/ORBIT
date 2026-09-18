using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Manages highlight video clips — creating clips with video upload or URL,
    /// viewing channel clips, platform-wide top clips, and recording clip views.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ClipController : ControllerBase
    {
        private readonly IClipService _clipService;

        public ClipController(IClipService clipService)
        {
            _clipService = clipService;
        }

        /// <summary>
        /// Slices the last N seconds (default 60s, max 300s / 5 min) from a live stream on the media server side.
        /// Authenticated users only.
        /// </summary>
        [HttpPost("slice")]
        [Authorize]
        [ProducesResponseType(typeof(ClipResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SliceClip([FromBody] SliceClipDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _clipService.SliceLiveClipAsync(userId, dto);
            return CreatedAtAction(nameof(GetClipById), new { clipId = result.Id }, result);
        }

        /// <summary>
        /// Creates a clip from a pre-generated video URL (e.g., after media server slicing).
        /// Accepts JSON body with video URL, title, channel, and optional metadata.
        /// </summary>
        [HttpPost]
        [HttpPost("create")]
        [Authorize]
        [ProducesResponseType(typeof(ClipResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateClip([FromBody] CreateClipDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _clipService.CreateClipAsync(userId, dto, null);
            return CreatedAtAction(nameof(GetClipById), new { clipId = result.Id }, result);
        }

        /// <summary>
        /// Gets all clips for a specific channel, ordered by newest first.
        /// </summary>
        [HttpGet("channel/{channelId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ClipResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetChannelClips(int channelId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _clipService.GetChannelClipsAsync(channelId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets the top watched clips across the entire platform, ordered by view count descending.
        /// </summary>
        [HttpGet("top")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ClipResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopClips([FromQuery] int count = 20)
        {
            var result = await _clipService.GetTopClipsAsync(count);
            return Ok(result);
        }

        /// <summary>
        /// Gets the details of a single clip by its ID.
        /// </summary>
        [HttpGet("{clipId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ClipResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetClipById(int clipId)
        {
            var result = await _clipService.GetClipByIdAsync(clipId);
            return Ok(result);
        }

        /// <summary>
        /// Records a unique view for a clip.
        /// Authenticated users are tracked by user ID (deduplicated).
        /// Anonymous users can provide a sessionId query parameter for deduplication.
        /// </summary>
        [HttpPost("{clipId:int}/view")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecordClipView(int clipId, [FromQuery] string? sessionId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            await _clipService.RecordClipViewAsync(clipId, userId, sessionId);
            return Ok(new { message = "View recorded." });
        }

        /// <summary>
        /// Deletes a clip. Only the clip creator, channel owner, or Admin can delete it.
        /// </summary>
        [HttpDelete("{clipId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteClip(int clipId)
        {
            var userId = GetUserId();
            await _clipService.DeleteClipAsync(clipId, userId);
            return Ok(new { message = "Clip deleted successfully." });
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
