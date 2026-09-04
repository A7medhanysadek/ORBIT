using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Chat;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// REST endpoints for chat history and VOD replay.
    /// Real-time chat is handled by the StreamChatHub (SignalR).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Gets all chat messages for a stream, ordered by stream offset.
        /// Used for loading the full chat history during VOD replay.
        /// </summary>
        [HttpGet("{streamId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStreamChat(int streamId)
        {
            var messages = await _chatService.GetStreamChatAsync(streamId);
            return Ok(messages);
        }

        /// <summary>
        /// Gets chat messages within a time window (seconds from stream start).
        /// Used for seeking in VOD — the player requests messages around the current playback position.
        /// 
        /// Example: GET /api/chat/5/range?from=120&amp;to=180
        ///   → returns messages sent between 2:00 and 3:00 into the stream
        /// </summary>
        [HttpGet("{streamId:int}/range")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreamChatRange(
            int streamId,
            [FromQuery] double from,
            [FromQuery] double to)
        {
            if (from < 0 || to < 0 || from > to)
                return BadRequest(new { message = "Invalid time range. 'from' must be >= 0 and <= 'to'." });

            var messages = await _chatService.GetStreamChatRangeAsync(streamId, from, to);
            return Ok(messages);
        }
    }
}
