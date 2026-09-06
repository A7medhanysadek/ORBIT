using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Moderation;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Chat moderation endpoints. All actions are per-channel.
    /// Both the channel owner and hired moderators can perform these actions.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Streamer,Moderator")]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        /// <summary>
        /// Deletes (soft-deletes) a chat message.
        /// The message is hidden from chat but preserved in the database.
        /// </summary>
        [HttpDelete("{channelId:int}/messages/{messageId:long}")]
        [ProducesResponseType(typeof(ModerationActionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteMessage(int channelId, long messageId)
        {
            var userId = GetUserId();
            var result = await _moderationService.DeleteMessageAsync(channelId, userId, messageId);
            return Ok(result);
        }

        /// <summary>
        /// Times out a user from chat in a specific channel.
        /// Duration must be within configured bounds (default: 10s–86400s).
        /// </summary>
        [HttpPost("{channelId:int}/timeout")]
        [ProducesResponseType(typeof(ModerationActionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> TimeoutUser(int channelId, [FromBody] TimeoutUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _moderationService.TimeoutUserAsync(channelId, userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Permanently bans a user from chat in a specific channel (until unbanned).
        /// </summary>
        [HttpPost("{channelId:int}/ban")]
        [ProducesResponseType(typeof(ModerationActionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BanUser(int channelId, [FromBody] BanUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _moderationService.BanUserAsync(channelId, userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Unbans a user from chat in a specific channel.
        /// </summary>
        [HttpDelete("{channelId:int}/ban/{username}")]
        [ProducesResponseType(typeof(ModerationActionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UnbanUser(int channelId, string username)
        {
            var userId = GetUserId();
            var result = await _moderationService.UnbanUserAsync(channelId, userId, username);
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
