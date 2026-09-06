using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;
using System.Security.Cryptography;

namespace OrbitBackend.Services
{
    public class StreamService : IStreamService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<StreamService> _logger;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly ViewerTracker _viewerTracker;

        public StreamService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            ILogger<StreamService> logger,
            IMediaServerConfigService mediaServerConfig,
            ViewerTracker viewerTracker)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _logger = logger;
            _mediaServerConfig = mediaServerConfig;
            _viewerTracker = viewerTracker;
        }

        public async Task<StreamKeyResponseDto> GenerateStreamKeyAsync(string userId)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You must create a channel before generating a stream key.");

            channel.StreamKey = GenerateSecureStreamKey();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream key generated for channel {ChannelId} (user {UserId}).", channel.Id, userId);

            return new StreamKeyResponseDto
            {
                StreamKey = channel.StreamKey,
                RtmpUrl = GetRtmpIngestUrl(),
                Message = "Stream key generated successfully. Use this key in your streaming software (e.g., OBS)."
            };
        }

        public async Task<StreamKeyResponseDto> GetStreamKeyAsync(string userId)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You must create a channel first.");

            if (string.IsNullOrEmpty(channel.StreamKey))
                throw new InvalidOperationException("No stream key found. Please generate one first.");

            return new StreamKeyResponseDto
            {
                StreamKey = channel.StreamKey,
                RtmpUrl = GetRtmpIngestUrl(),
                Message = "Use this stream key in your streaming software (e.g., OBS)."
            };
        }

        public async Task<StreamResponseDto> CreateStreamAsync(string userId, CreateStreamDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You must create a channel before creating a stream.");

            if (string.IsNullOrEmpty(channel.StreamKey))
                throw new InvalidOperationException("You must generate a stream key before creating a stream.");

            // Check for existing active stream
            var existingLive = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == userId && s.IsLive);

            if (existingLive)
                throw new InvalidOperationException("You already have an active live stream. End it before creating a new one.");

            var stream = new LiveStream
            {
                Title = dto.Title,
                Description = dto.Description,
                IsLive = false,
                StreamerId = userId,
                ChannelId = channel.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.LiveStreams.Add(stream);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} created by user {UserId} on channel {ChannelId}.", stream.Id, userId, channel.Id);

            return MapToResponseDto(stream, user, channel);
        }

        public async Task<bool> ValidateStreamKeyAsync(string streamKey)
        {
            if (string.IsNullOrEmpty(streamKey))
                return false;

            var channel = await _context.Channels
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.StreamKey == streamKey);

            if (channel == null)
            {
                _logger.LogWarning("Stream key validation failed — key not found.");
                return false;
            }

            var user = channel.Owner;

            // Check Streamer role
            var isStreamer = await _userManager.IsInRoleAsync(user, "Streamer");
            if (!isStreamer)
            {
                _logger.LogWarning("Stream key validation failed — user {UserId} is not a Streamer.", user.Id);
                return false;
            }

            // Allow reconnection: check for a disconnected stream first
            var hasDisconnectedStream = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == user.Id && s.IsLive && s.DisconnectedAt != null);

            if (hasDisconnectedStream)
            {
                _logger.LogInformation("Stream key validated for reconnection — user {UserId}.", user.Id);
                return true;
            }

            // Check for a pending (created but not yet live) stream
            var hasPendingStream = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null);

            if (!hasPendingStream)
            {
                _logger.LogWarning("Stream key validation failed — no pending or disconnected stream for user {UserId}.", user.Id);
                return false;
            }

            _logger.LogInformation("Stream key validated for user {UserId}.", user.Id);
            return true;
        }

        public async Task MarkStreamLiveAsync(string streamKey)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.StreamKey == streamKey)
                ?? throw new InvalidOperationException("Invalid stream key.");

            var user = channel.Owner;

            // Priority 1: Reconnect to a disconnected stream (grace period reconnection)
            var disconnectedStream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && s.IsLive && s.DisconnectedAt != null)
                .OrderByDescending(s => s.DisconnectedAt)
                .FirstOrDefaultAsync();

            if (disconnectedStream != null)
            {
                disconnectedStream.DisconnectedAt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Stream {StreamId} RECONNECTED for user {UserId}. Session continues.",
                    disconnectedStream.Id, user.Id);
                return;
            }

            // Priority 2: Start a new pending stream
            var pendingStream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No pending stream found for this streamer.");

            pendingStream.IsLive = true;
            pendingStream.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} is now LIVE for user {UserId}.", pendingStream.Id, user.Id);
        }

        public async Task MarkStreamOfflineAsync(string streamKey)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.StreamKey == streamKey);

            if (channel == null)
            {
                _logger.LogWarning("on_publish_done received for unknown stream key.");
                return;
            }

            var user = channel.Owner;

            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && s.IsLive)
                .FirstOrDefaultAsync();

            if (stream == null)
            {
                _logger.LogWarning("on_publish_done received but no live stream found for user {UserId}.", user.Id);
                return;
            }

            // Instead of ending the stream, mark it as disconnected.
            // The StreamGracePeriodService will auto-end it if the streamer doesn't reconnect.
            stream.DisconnectedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Stream {StreamId} DISCONNECTED for user {UserId}. Grace period started.",
                stream.Id, user.Id);
        }

        public async Task EndStreamAsync(string userId)
        {
            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == userId && s.IsLive)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active live stream found.");

            stream.IsLive = false;
            stream.EndedAt = DateTime.UtcNow;
            stream.DisconnectedAt = null; // Clear if set
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} manually ended by user {UserId}.", stream.Id, userId);
        }

        public async Task<List<LiveStreamSummaryDto>> GetLiveStreamsAsync()
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            var liveStreams = await _context.LiveStreams
                .Where(s => s.IsLive)
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .ToListAsync();

            return liveStreams.Select(s => new LiveStreamSummaryDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                StreamerName = s.Streamer.FullName,
                ChannelName = s.Channel.ChannelName,
                HlsUrl = $"{hlsBaseUrl}/{s.Channel.StreamKey}.m3u8",
                StartedAt = s.StartedAt,
                IsReconnecting = s.DisconnectedAt != null,
                ViewerCount = _viewerTracker.GetViewerCount(s.Id)
            }).ToList();
        }

        public async Task<StreamResponseDto> GetStreamByIdAsync(int id)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new InvalidOperationException("Stream not found.");

            return MapToResponseDto(stream, stream.Streamer, stream.Channel);
        }

        public async Task SaveRecordingPathAsync(string streamKey, string filePath)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.StreamKey == streamKey);

            if (channel == null)
            {
                _logger.LogWarning("on_record_done received for unknown stream key.");
                return;
            }

            // Find the most recently ended stream for this channel
            var stream = await _context.LiveStreams
                .Where(s => s.ChannelId == channel.Id && !s.IsLive && s.EndedAt != null)
                .OrderByDescending(s => s.EndedAt)
                .FirstOrDefaultAsync();

            if (stream == null)
            {
                _logger.LogWarning("on_record_done received but no ended stream found for channel {ChannelId}.", channel.Id);
                return;
            }

            // Extract just the file name from the full path
            stream.RecordingFileName = System.IO.Path.GetFileName(filePath);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Recording saved for stream {StreamId}: {FileName}",
                stream.Id, stream.RecordingFileName);
        }

        private static string GenerateSecureStreamKey()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private string GetRtmpIngestUrl()
        {
            return _mediaServerConfig.GetRtmpUrl();
        }

        private StreamResponseDto MapToResponseDto(LiveStream stream, AppUser streamer, Channel channel)
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            // Build VOD URL if the stream has a recording
            string? vodUrl = null;
            if (!stream.IsLive && !string.IsNullOrEmpty(stream.RecordingFileName))
            {
                var recordingsBaseUrl = _config["Streaming:RecordingsBaseUrl"]
                    ?? hlsBaseUrl.Replace("/hls", "/recordings");
                vodUrl = $"{recordingsBaseUrl}/{stream.RecordingFileName}";
            }

            return new StreamResponseDto
            {
                Id = stream.Id,
                Title = stream.Title,
                Description = stream.Description,
                IsLive = stream.IsLive,
                HlsUrl = stream.IsLive ? $"{hlsBaseUrl}/{channel.StreamKey}.m3u8" : null,
                VodUrl = vodUrl,
                DisconnectedAt = stream.DisconnectedAt,
                ViewerCount = _viewerTracker.GetViewerCount(stream.Id),
                StreamerId = stream.StreamerId,
                StreamerName = streamer.FullName,
                ChannelName = channel.ChannelName,
                ChannelId = channel.Id,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                CreatedAt = stream.CreatedAt
            };
        }
    }
}
