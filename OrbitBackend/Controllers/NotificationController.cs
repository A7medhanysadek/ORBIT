using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Notification;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Manages account notifications for live broadcasts, new followers, and moderation assignments.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Gets the current user's recent notifications.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<NotificationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNotifications([FromQuery] int count = 30)
        {
            var userId = GetUserId();
            var result = await _notificationService.GetUserNotificationsAsync(userId, count);
            return Ok(result);
        }

        /// <summary>
        /// Gets the count of unread notifications for the current user.
        /// </summary>
        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId();
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new UnreadCountDto { UnreadCount = count });
        }

        /// <summary>
        /// Marks a specific notification as read.
        /// </summary>
        [HttpPut("{id:int}/read")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetUserId();
            var success = await _notificationService.MarkAsReadAsync(userId, id);
            if (!success)
                return NotFound(new { message = "Notification not found." });

            return Ok(new { message = "Notification marked as read." });
        }

        /// <summary>
        /// Marks all notifications for the current user as read.
        /// </summary>
        [HttpPut("read-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read." });
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
