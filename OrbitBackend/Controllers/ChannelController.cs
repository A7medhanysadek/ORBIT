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
    /// Also handles channel customization: profile, photos, social links, donation info.
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
        [Authorize]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyChannel()
        {
            var userId = GetUserId();
            try
            {
                var result = await _channelService.GetMyChannelAsync(userId);
                return Ok(result);
            }
            catch (InvalidOperationException)
            {
                return NotFound(new { message = "You don't have a channel." });
            }
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
        /// Searches channels by channel name, description, or owner. Public endpoint.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<OrbitBackend.DTOs.Admin.ChannelSearchResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchChannels([FromQuery] string q = "")
        {
            var result = await _channelService.SearchChannelsAsync(q);
            return Ok(result);
        }

        // ── Moderator Management ──

        /// <summary>
        /// Hires a moderator for the channel by their username.
        /// Only the channel owner can hire moderators.
        /// </summary>
        [HttpPost("moderators/hire")]
        [Authorize(Roles = "Streamer,Admin")]
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
        [Authorize(Roles = "Streamer,Admin")]
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

        // ── Channel Customization ──

        /// <summary>
        /// Updates channel profile information (description, donation URL/message, save streams toggle).
        /// </summary>
        [HttpPut("profile")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateChannelProfile([FromBody] UpdateChannelProfileDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _channelService.UpdateChannelProfileAsync(userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Uploads a channel profile photo (avatar). Accepts JPEG, PNG, WebP, GIF (max 5MB).
        /// </summary>
        [HttpPost("photo")]
        [Authorize(Roles = "Streamer")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadChannelPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var userId = GetUserId();
            var result = await _channelService.UploadChannelPhotoAsync(userId, file);
            return Ok(result);
        }

        /// <summary>
        /// Uploads a channel cover/banner image. Accepts JPEG, PNG, WebP, GIF (max 5MB).
        /// </summary>
        [HttpPost("cover")]
        [Authorize(Roles = "Streamer")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadChannelCover(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var userId = GetUserId();
            var result = await _channelService.UploadChannelCoverAsync(userId, file);
            return Ok(result);
        }

        /// <summary>
        /// Sets all social media links for the channel (replaces existing links).
        /// </summary>
        [HttpPut("social-links")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(List<ChannelSocialLinkDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateSocialLinks([FromBody] UpdateChannelSocialLinksDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _channelService.UpdateSocialLinksAsync(userId, dto);
            return Ok(result);
        }

        /// <summary>
        /// Gets social media links for any channel by its ID.
        /// </summary>
        [HttpGet("{id:int}/social-links")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ChannelSocialLinkDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSocialLinks(int id)
        {
            var result = await _channelService.GetSocialLinksAsync(id);
            return Ok(result);
        }

        // ── Save Streams Toggle ──

        /// <summary>
        /// Toggles whether ended streams are saved as VODs for this channel.
        /// </summary>
        [HttpPut("save-streams")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(ChannelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ToggleSaveStreams([FromBody] ToggleSaveStreamsDto dto)
        {
            var userId = GetUserId();
            var result = await _channelService.UpdateChannelProfileAsync(userId, new UpdateChannelProfileDto
            {
                SaveStreams = dto.SaveStreams
            });
            return Ok(result);
        }

        // ── Following ──

        /// <summary>
        /// Toggles following a channel. If currently following, unfollows. If not following, follows.
        /// </summary>
        [HttpPost("{id:int}/follow")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ToggleFollow(int id)
        {
            var userId = GetUserId();
            var isFollowing = await _channelService.ToggleFollowAsync(userId, id);
            return Ok(new { isFollowing, channelId = id });
        }

        /// <summary>
        /// Checks if the authenticated user is following a channel.
        /// </summary>
        [HttpGet("{id:int}/following")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> IsFollowing(int id)
        {
            var userId = GetUserId();
            var isFollowing = await _channelService.IsFollowingAsync(userId, id);
            return Ok(new { isFollowing, channelId = id });
        }

        /// <summary>
        /// Gets the list of channels followed by the authenticated user.
        /// </summary>
        [HttpGet("following")]
        [Authorize]
        [ProducesResponseType(typeof(List<ChannelFollowDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetFollowedChannels()
        {
            var userId = GetUserId();
            var followed = await _channelService.GetFollowedChannelsAsync(userId);
            return Ok(followed);
        }

        /// <summary>
        /// Gets custom emojis for a channel by channel ID. Public endpoint for chat & viewer clients.
        /// </summary>
        [HttpGet("{id:int}/emojis")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<OrbitBackend.DTOs.Dashboard.CustomEmojiResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChannelEmojis(int id)
        {
            var emojis = await _channelService.GetChannelEmojisAsync(id);
            return Ok(emojis);
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

    /// <summary>
    /// Simple DTO for the save-streams toggle endpoint.
    /// </summary>
    public class ToggleSaveStreamsDto
    {
        public bool SaveStreams { get; set; }
    }
}
