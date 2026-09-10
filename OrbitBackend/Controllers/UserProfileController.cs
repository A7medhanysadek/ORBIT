using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.UserProfile;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Manages user profile pictures and public profile data.
    /// </summary>
    [ApiController]
    [Route("api/user-profile")]
    [Produces("application/json")]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileService _userProfileService;

        public UserProfileController(IUserProfileService userProfileService)
        {
            _userProfileService = userProfileService;
        }

        /// <summary>
        /// Uploads or replaces the authenticated user's profile picture.
        /// Accepts JPEG, PNG, WebP, GIF (max 5MB).
        /// </summary>
        [HttpPost("picture")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UpdateProfilePictureResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var userId = GetUserId();
            var result = await _userProfileService.UploadProfilePictureAsync(userId, file);
            return Ok(result);
        }

        /// <summary>
        /// Removes the authenticated user's profile picture.
        /// </summary>
        [HttpDelete("picture")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RemoveProfilePicture()
        {
            var userId = GetUserId();
            await _userProfileService.RemoveProfilePictureAsync(userId);
            return NoContent();
        }

        /// <summary>
        /// Gets a user's public profile (name, picture, channel info).
        /// </summary>
        [HttpGet("{userId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(UserPublicProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPublicProfile(string userId)
        {
            var result = await _userProfileService.GetPublicProfileAsync(userId);
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
