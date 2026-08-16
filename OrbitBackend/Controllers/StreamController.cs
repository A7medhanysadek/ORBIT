using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class StreamController : ControllerBase
    {
        private readonly IStreamService _streamService;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly IConfiguration _config;

        public StreamController(IStreamService streamService, IMediaServerConfigService mediaServerConfig, IConfiguration config)
        {
            _streamService = streamService;
            _mediaServerConfig = mediaServerConfig;
            _config = config;
        }

        
        
        

        
        
        
        [HttpPost("key/generate")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(StreamKeyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GenerateStreamKey()
        {
            var userId = GetUserId();
            var result = await _streamService.GenerateStreamKeyAsync(userId);
            return Ok(result);
        }

        
        
        
        [HttpGet("key")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(StreamKeyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetStreamKey()
        {
            var userId = GetUserId();
            var result = await _streamService.GetStreamKeyAsync(userId);
            return Ok(result);
        }

        
        
        
        [HttpPost("create")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(typeof(StreamResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateStream([FromBody] CreateStreamDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            var result = await _streamService.CreateStreamAsync(userId, dto);
            return CreatedAtAction(nameof(GetStreamById), new { id = result.Id }, result);
        }

        
        
        
        [HttpPost("end")]
        [Authorize(Roles = "Streamer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> EndStream()
        {
            var userId = GetUserId();
            await _streamService.EndStreamAsync(userId);
            return Ok(new { message = "Stream ended successfully." });
        }

        
        
        

        
        
        
        [HttpGet("live")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<LiveStreamSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLiveStreams()
        {
            var result = await _streamService.GetLiveStreamsAsync();
            return Ok(result);
        }

        
        
        
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(StreamResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreamById(int id)
        {
            var result = await _streamService.GetStreamByIdAsync(id);
            return Ok(result);
        }

        
        
        

        
        
        
        
        [HttpPost("rtmp/on-publish")]
        [AllowAnonymous]
        [Consumes("application/x-www-form-urlencoded")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ApiExplorerSettings(IgnoreApi = true)] 
        public async Task<IActionResult> OnPublish([FromForm] RtmpCallbackDto dto)
        {
            if (!ValidateCallbackSecret(dto.Secret))
                return StatusCode(StatusCodes.Status403Forbidden);

            var streamKey = dto.Name;
            if (string.IsNullOrEmpty(streamKey))
                return StatusCode(StatusCodes.Status403Forbidden);

            var isValid = await _streamService.ValidateStreamKeyAsync(streamKey);
            if (!isValid)
                return StatusCode(StatusCodes.Status403Forbidden);

            await _streamService.MarkStreamLiveAsync(streamKey);
            return Ok();
        }

        
        
        
        [HttpPost("rtmp/on-publish-done")]
        [AllowAnonymous]
        [Consumes("application/x-www-form-urlencoded")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ApiExplorerSettings(IgnoreApi = true)] 
        public async Task<IActionResult> OnPublishDone([FromForm] RtmpCallbackDto dto)
        {
            if (!ValidateCallbackSecret(dto.Secret))
                return StatusCode(StatusCodes.Status403Forbidden);

            var streamKey = dto.Name;
            if (!string.IsNullOrEmpty(streamKey))
                await _streamService.MarkStreamOfflineAsync(streamKey);

            return Ok();
        }

        

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Could not identify the user from the token.");

            return userId;
        }

        private bool ValidateCallbackSecret(string? secret)
        {
            var expectedSecret = _config["Streaming:CallbackSecret"];
            if (string.IsNullOrEmpty(expectedSecret))
                return true; 

            return string.Equals(secret, expectedSecret, StringComparison.Ordinal);
        }

        
        
        
        

        
        
        
        
        [HttpPost("server/set-url")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult SetMediaServerUrl([FromBody] SetMediaServerUrlDto dto)
        {
            var result = _mediaServerConfig.SetUrls(dto.RtmpUrl, dto.HlsBaseUrl);
            return Ok(result);
        }

        
        
        
        
        [HttpPost("server/clear-url")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult ClearMediaServerUrl()
        {
            var result = _mediaServerConfig.ClearUrls();
            return Ok(result);
        }

        
        
        
        [HttpGet("server/config")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(MediaServerConfigDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetMediaServerConfig()
        {
            var result = _mediaServerConfig.GetConfig();
            return Ok(result);
        }
    }
}
