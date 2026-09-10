using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Dashboard;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Hubs;
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
        private readonly IHubContext<StreamChatHub> _hubContext;
        private readonly HttpClient _httpClient;

        public StreamService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            ILogger<StreamService> logger,
            IMediaServerConfigService mediaServerConfig,
            ViewerTracker viewerTracker,
            IHubContext<StreamChatHub> hubContext,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _logger = logger;
            _mediaServerConfig = mediaServerConfig;
            _viewerTracker = viewerTracker;
            _hubContext = hubContext;
            _httpClient = httpClientFactory.CreateClient();
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

            // Validate category if provided
            if (dto.CategoryId.HasValue)
            {
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId.Value);
                if (!categoryExists)
                    throw new InvalidOperationException("The specified category does not exist.");
            }

            var stream = new LiveStream
            {
                Title = dto.Title,
                Description = dto.Description,
                IsLive = false,
                StreamerId = userId,
                ChannelId = channel.Id,
                CategoryId = dto.CategoryId,
                CreatedAt = DateTime.UtcNow
            };

            _context.LiveStreams.Add(stream);
            await _context.SaveChangesAsync();

            // Reload with category
            if (stream.CategoryId.HasValue)
            {
                await _context.Entry(stream).Reference(s => s.Category).LoadAsync();
            }

            _logger.LogInformation("Stream {StreamId} created by user {UserId} on channel {ChannelId}.", stream.Id, userId, channel.Id);

            return MapToResponseDto(stream, user, channel);
        }

        public async Task<StreamResponseDto> UpdateStreamAsync(string userId, UpdateLiveStreamDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            var stream = await _context.LiveStreams
                .Include(s => s.Channel)
                .Include(s => s.Category)
                .Where(s => s.StreamerId == userId && (s.IsLive || s.EndedAt == null))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active or pending stream found to update.");

            if (dto.CategoryId.HasValue)
            {
                var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId.Value)
                    ?? throw new InvalidOperationException("The specified category does not exist.");
                stream.CategoryId = category.Id;
                stream.Category = category;
            }

            if (!string.IsNullOrWhiteSpace(dto.Title))
            {
                stream.Title = dto.Title.Trim();
            }

            if (dto.Description != null)
            {
                stream.Description = dto.Description.Trim();
            }

            await _context.SaveChangesAsync();

            // Broadcast real-time update over SignalR to all viewers watching this stream
            try
            {
                await _hubContext.Clients.Group($"stream_{stream.Id}").SendAsync("StreamUpdated", new
                {
                    streamId = stream.Id,
                    title = stream.Title,
                    description = stream.Description,
                    categoryId = stream.CategoryId,
                    categoryName = stream.Category?.Name,
                    categorySlug = stream.Category?.Slug
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast StreamUpdated SignalR event for stream {StreamId}", stream.Id);
            }

            _logger.LogInformation("Stream {StreamId} metadata updated by user {UserId}.", stream.Id, userId);

            return MapToResponseDto(stream, user, stream.Channel);
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

        public async Task<MarkLiveResultDto?> MarkStreamLiveAsync(string streamKey)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.StreamKey == streamKey)
                ?? throw new InvalidOperationException("Invalid stream key.");

            var user = channel.Owner;

            // Priority 1: Reconnect to a disconnected stream (grace period reconnection)
            var disconnectedStream = await _context.LiveStreams
                .Include(s => s.Category)
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

                return new MarkLiveResultDto
                {
                    StreamId = disconnectedStream.Id,
                    Title = disconnectedStream.Title,
                    CategoryId = disconnectedStream.CategoryId,
                    CategoryName = disconnectedStream.Category?.Name
                };
            }

            // Priority 2: Start a new pending stream
            var pendingStream = await _context.LiveStreams
                .Include(s => s.Category)
                .Where(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No pending stream found for this streamer.");

            pendingStream.IsLive = true;
            pendingStream.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} is now LIVE for user {UserId}.", pendingStream.Id, user.Id);

            return new MarkLiveResultDto
            {
                StreamId = pendingStream.Id,
                Title = pendingStream.Title,
                CategoryId = pendingStream.CategoryId,
                CategoryName = pendingStream.Category?.Name
            };
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

            // Mark disconnected so reconnect grace period applies
            stream.DisconnectedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Stream {StreamId} DISCONNECTED for user {UserId}. Grace period started.",
                stream.Id, user.Id);
        }

        public async Task<StreamSessionSummaryDto> EndStreamAsync(string userId)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Channel)
                .Include(s => s.ChatMessages)
                .Where(s => s.StreamerId == userId && (s.IsLive || s.EndedAt == null))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active or pending stream found.");

            var channel = stream.Channel;
            var now = DateTime.UtcNow;
            var startedAt = stream.StartedAt ?? stream.CreatedAt;
            var durationSeconds = stream.StartedAt.HasValue ? (now - stream.StartedAt.Value).TotalSeconds : 0;

            // Save peak viewers
            var peak = _viewerTracker.GetPeakViewerCount(stream.Id);
            stream.PeakViewers = Math.Max(stream.PeakViewers, peak);

            // 1. Drop RTMP publisher in NGINX FIRST so OBS disconnects immediately and NGINX finalizes recording
            if (!string.IsNullOrEmpty(channel.StreamKey))
            {
                await TryDropNginxPublisherAsync(channel.StreamKey);
                // Brief pause to allow NGINX on_record_done webhook to be processed
                await Task.Delay(400);
                await _context.Entry(stream).ReloadAsync();
            }

            stream.IsLive = false;
            stream.EndedAt = now;
            stream.DisconnectedAt = null;
            await _context.SaveChangesAsync();

            // Broadcast StreamEnded via SignalR to room viewers
            try
            {
                await _hubContext.Clients.Group($"stream_{stream.Id}").SendAsync("StreamEnded", stream.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast StreamEnded SignalR event for stream {StreamId}", stream.Id);
            }

            // Clean up viewer tracker
            _viewerTracker.ClearStream(stream.Id);

            _logger.LogInformation("Stream {StreamId} manually ended by user {UserId}. Peak viewers: {Peak}, Recording: {Recording}",
                stream.Id, userId, stream.PeakViewers, stream.RecordingFileName ?? "None");

            var recordingsBaseUrl = _mediaServerConfig.GetRecordingsBaseUrl();

            return new StreamSessionSummaryDto
            {
                StreamId = stream.Id,
                Title = stream.Title,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                DurationSeconds = durationSeconds,
                FormattedDuration = FormatDuration(durationSeconds),
                PeakViewers = stream.PeakViewers,
                TotalChatMessages = stream.ChatMessages.Count(m => !m.IsDeleted),
                IsSavedAsVod = !string.IsNullOrEmpty(stream.RecordingFileName),
                VodUrl = !string.IsNullOrEmpty(stream.RecordingFileName) ? $"{recordingsBaseUrl}/{stream.RecordingFileName}" : null,
                Message = "Stream ended successfully."
            };
        }

        public async Task<List<LiveStreamSummaryDto>> GetLiveStreamsAsync()
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            var liveStreams = await _context.LiveStreams
                .Where(s => s.IsLive)
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .Include(s => s.Category)
                .ToListAsync();

            return liveStreams.Select(s => new LiveStreamSummaryDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                StreamerName = s.Streamer.FullName,
                ChannelName = s.Channel.ChannelName,
                ChannelId = s.ChannelId,
                HlsUrl = $"{hlsBaseUrl}/{s.Channel.StreamKey}.m3u8",
                ThumbnailUrl = s.ThumbnailUrl,
                StartedAt = s.StartedAt,
                CategoryId = s.CategoryId,
                CategoryName = s.Category?.Name,
                CategorySlug = s.Category?.Slug,
                IsReconnecting = s.DisconnectedAt != null,
                ViewerCount = _viewerTracker.GetViewerCount(s.Id)
            }).ToList();
        }

        public async Task<StreamResponseDto> GetStreamByIdAsync(int id)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .Include(s => s.Category)
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

            // If the streamer has disabled stream saving, do not record or save VOD
            if (!channel.SaveStreams)
            {
                _logger.LogInformation("Channel {ChannelId} has SaveStreams disabled. Skipping VOD save.", channel.Id);
                return;
            }

            // Find the active or most recent stream for this channel
            var stream = await _context.LiveStreams
                .Where(s => s.ChannelId == channel.Id)
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();

            if (stream == null)
            {
                _logger.LogWarning("on_record_done received but no stream found for channel {ChannelId}.", channel.Id);
                return;
            }

            stream.RecordingFileName = System.IO.Path.GetFileName(filePath);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Recording saved for stream {StreamId}: {FileName} (from NGINX webhook)",
                stream.Id, stream.RecordingFileName);
        }

        private async Task TryDropNginxPublisherAsync(string streamKey)
        {
            try
            {
                var baseControlUrl = _mediaServerConfig.GetControlUrl();
                var controlUrl = $"{baseControlUrl}/drop/publisher?app=live&name={Uri.EscapeDataString(streamKey)}";
                var response = await _httpClient.GetAsync(controlUrl);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Dropped RTMP publisher in NGINX via {ControlUrl}.", controlUrl);
                }
                else
                {
                    _logger.LogWarning("Failed to drop RTMP publisher in NGINX: HTTP {StatusCode} from {ControlUrl}", response.StatusCode, controlUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not call NGINX drop publisher control endpoint.");
            }
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

        private static string FormatDuration(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
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
                ThumbnailUrl = stream.ThumbnailUrl,
                VodUrl = vodUrl,
                DisconnectedAt = stream.DisconnectedAt,
                ViewerCount = _viewerTracker.GetViewerCount(stream.Id),
                StreamerId = stream.StreamerId,
                StreamerName = streamer.FullName,
                ChannelName = channel.ChannelName,
                ChannelId = channel.Id,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                CreatedAt = stream.CreatedAt,
                CategoryId = stream.CategoryId,
                CategoryName = stream.Category?.Name,
                CategorySlug = stream.Category?.Slug
            };
        }
    }
}
