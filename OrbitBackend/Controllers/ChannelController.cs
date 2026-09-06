using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Channel;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Manages channels. Users create a channel to become streamers.
    /// Channel owners can hire and manage moderators.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ChannelController : ControllerBase
    {
        private readonly IChannelService _channelService;

        public ChannelController(IChannelService channelService)
        {
            _channelService = channelService;
        }

        /// <summary>
        /// Creates a channel for the authenticated user.
        /// This automatically grants the Streamer role, enabling streaming.
        /// Each user can only have one channel.
        /// </summary>
        [HttpPost("create")]
        [Authorize]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateChannel([FromBody] CreateChannelDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _channelService.CreateChannelAsync(userId, dto);
            return CreatedAtAction(nameof(GetChannelById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Gets the channel owned by the authenticated user.
        /// </summary>
        [HttpGet("me")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyChannel()
        {
            var userId = GetUserId();
            var result = await _channelService.GetMyChannelAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Gets a channel by its ID. Public endpoint.
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetChannelById(int id)
        {
            var result = await _channelService.GetChannelByIdAsync(id);
            return Ok(result);
        }

        /// <summary>
        /// Hires a moderator for the channel by their username.
        /// Only the channel owner can hire moderators.
        /// </summary>
        [HttpPost("moderators/hire")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(ChannelModeratorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> HireModerator([FromBody] HireModeratorDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _channelService.HireModeratorAsync(userId, dto);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Removes a moderator from the channel by their username.
        /// Only the channel owner can remove moderators.
        /// </summary>
        [HttpDelete("moderators/{username}")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveModerator(string username)
        {
            var userId = GetUserId();
            await _channelService.RemoveModeratorAsync(userId, username);
            return NoContent();
        }

        /// <summary>
        /// Lists all moderators for the authenticated user's channel.
        /// </summary>
        [HttpGet("moderators")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(List<ChannelModeratorDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetModerators()
        {
            var userId = GetUserId();
            var result = await _channelService.GetModeratorsAsync(userId);
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
