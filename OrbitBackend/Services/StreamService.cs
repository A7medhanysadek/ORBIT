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
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

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
        private readonly INotificationService _notificationService;

        public StreamService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            ILogger<StreamService> logger,
            IMediaServerConfigService mediaServerConfig,
            ViewerTracker viewerTracker,
            IHubContext<StreamChatHub> hubContext,
            IHttpClientFactory httpClientFactory,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _logger = logger;
            _mediaServerConfig = mediaServerConfig;
            _viewerTracker = viewerTracker;
            _hubContext = hubContext;
            _httpClient = httpClientFactory.CreateClient();
            _notificationService = notificationService;
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

            // Allow reconnection during nginx grace period (drop_idle_publisher):
            // When nginx keeps the session alive, on_publish_done hasn't fired yet,
            // so the stream is still IsLive with DisconnectedAt == null.
            // Allow the publisher to re-publish to the same key.
            var hasActiveStream = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == user.Id && s.IsLive && s.DisconnectedAt == null);

            if (hasActiveStream)
            {
                _logger.LogInformation("Stream key validated — active stream found (nginx grace period reconnection) for user {UserId}.", user.Id);
                return true;
            }

            // Check for a pending (created but not yet live) stream
            var hasPendingStream = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null);

            if (!hasPendingStream)
            {
                // Auto-create a pending stream session so streamers can start streaming directly from OBS
                var autoStream = new LiveStream
                {
                    Title = $"{channel.ChannelName ?? user.UserName}'s Live Stream",
                    Description = "Live broadcast on Orbit",
                    IsLive = false,
                    StreamerId = user.Id,
                    ChannelId = channel.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.LiveStreams.Add(autoStream);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Auto-created stream session {StreamId} for user {UserId} upon direct OBS publish.", autoStream.Id, user.Id);
                return true;
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
                disconnectedStream.RecordingFileName = null; // Clear old chunk so live stream doesn't hold stale chunk
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Stream {StreamId} RECONNECTED for user {UserId}. Session continues.",
                    disconnectedStream.Id, user.Id);

                // Broadcast stream reconnected / live event to room
                try
                {
                    var hlsUrl = $"{_mediaServerConfig.GetHlsBaseUrl()}/{channel.StreamKey}.m3u8";
                    await _hubContext.Clients.Group($"stream_{disconnectedStream.Id}").SendAsync("StreamStarted", new
                    {
                        streamId = disconnectedStream.Id,
                        isLive = true,
                        hlsUrl = hlsUrl,
                        title = disconnectedStream.Title,
                        categoryName = disconnectedStream.Category?.Name
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast StreamStarted for reconnected stream {StreamId}", disconnectedStream.Id);
                }

                return new MarkLiveResultDto
                {
                    StreamId = disconnectedStream.Id,
                    Title = disconnectedStream.Title,
                    CategoryId = disconnectedStream.CategoryId,
                    CategoryName = disconnectedStream.Category?.Name
                };
            }

            // Priority 2: Reconnect during nginx grace period (drop_idle_publisher)
            // on_publish_done hasn't fired yet, stream is still IsLive with no DisconnectedAt
            var activeStream = await _context.LiveStreams
                .Include(s => s.Category)
                .Where(s => s.StreamerId == user.Id && s.IsLive && s.DisconnectedAt == null)
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeStream != null)
            {
                _logger.LogInformation(
                    "Stream {StreamId} RECONNECTED (nginx grace period) for user {UserId}. Session continues.",
                    activeStream.Id, user.Id);

                try
                {
                    var hlsUrl = $"{_mediaServerConfig.GetHlsBaseUrl()}/{channel.StreamKey}.m3u8";
                    await _hubContext.Clients.Group($"stream_{activeStream.Id}").SendAsync("StreamStarted", new
                    {
                        streamId = activeStream.Id,
                        isLive = true,
                        hlsUrl = hlsUrl,
                        title = activeStream.Title,
                        categoryName = activeStream.Category?.Name
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast StreamStarted for grace period stream {StreamId}", activeStream.Id);
                }

                return new MarkLiveResultDto
                {
                    StreamId = activeStream.Id,
                    Title = activeStream.Title,
                    CategoryId = activeStream.CategoryId,
                    CategoryName = activeStream.Category?.Name
                };
            }

            // Priority 3: Start a new pending stream
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

            // Broadcast stream live event to all connected watchers in the stream chat group
            try
            {
                var hlsUrl = $"{_mediaServerConfig.GetHlsBaseUrl()}/{channel.StreamKey}.m3u8";
                await _hubContext.Clients.Group($"stream_{pendingStream.Id}").SendAsync("StreamStarted", new
                {
                    streamId = pendingStream.Id,
                    isLive = true,
                    hlsUrl = hlsUrl,
                    title = pendingStream.Title,
                    categoryName = pendingStream.Category?.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast StreamStarted for stream {StreamId}", pendingStream.Id);
            }

            // Notify all followers that this channel is now live
            try
            {
                await _notificationService.NotifyFollowersStreamLiveAsync(
                    pendingStream.Id,
                    channel.Id,
                    channel.ChannelName,
                    pendingStream.Title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send live stream notification for stream {StreamId}", pendingStream.Id);
            }

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

            // If the stream was never live and never broadcasted (pending session before OBS):
            if (!stream.IsLive && stream.StartedAt == null)
            {
                stream.IsLive = false;
                stream.EndedAt = now;
                stream.DisconnectedAt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pending stream session {StreamId} ended/cancelled before OBS broadcast started.", stream.Id);

                return new StreamSessionSummaryDto
                {
                    StreamId = stream.Id,
                    Title = stream.Title,
                    StartedAt = null,
                    EndedAt = now,
                    DurationSeconds = 0,
                    FormattedDuration = "00:00:00",
                    PeakViewers = 0,
                    TotalChatMessages = 0,
                    IsSavedAsVod = false,
                    VodUrl = null,
                    Message = "Pending stream session ended successfully."
                };
            }

            var startedAt = stream.StartedAt ?? stream.CreatedAt;
            var durationSeconds = (now - startedAt).TotalSeconds;

            // Save peak viewers
            var peak = _viewerTracker.GetPeakViewerCount(stream.Id);
            stream.PeakViewers = Math.Max(stream.PeakViewers, peak);

            // 1. Drop RTMP publisher in NGINX FIRST so OBS disconnects immediately and NGINX finalizes recording
            if (!string.IsNullOrEmpty(channel.StreamKey))
            {
                await TryDropNginxPublisherAsync(channel.StreamKey);

                // Wait up to 1500ms for NGINX on_record_done webhook to be processed
                for (int i = 0; i < 6; i++)
                {
                    await Task.Delay(250);
                    await _context.Entry(stream).ReloadAsync();
                    if (!string.IsNullOrEmpty(stream.RecordingFileName))
                        break;
                }

                // Finalize and merge all session recording chunks into one file
                if (channel.SaveStreams)
                {
                    await FinalizeStreamRecordingAsync(stream, channel);
                }
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

            return liveStreams.Select(s =>
            {
                var (ytUrl, ytId, cleanDesc) = ParseYoutubeSimulated(s.Description);
                var isSimulated = !string.IsNullOrEmpty(ytUrl);
                var effectiveHls = isSimulated ? ytUrl : $"{hlsBaseUrl}/{s.Channel.StreamKey}.m3u8";
                var effectiveThumb = isSimulated && !string.IsNullOrEmpty(ytId)
                    ? $"https://img.youtube.com/vi/{ytId}/hqdefault.jpg"
                    : (s.ThumbnailUrl ?? $"{hlsBaseUrl}/{s.Channel.StreamKey}-preview.jpg");

                return new LiveStreamSummaryDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = cleanDesc,
                    StreamerName = !string.IsNullOrEmpty(s.Channel?.ChannelName) ? s.Channel.ChannelName : s.Streamer.FullName,
                    ChannelName = s.Channel?.ChannelName,
                    ChannelId = s.ChannelId,
                    HlsUrl = effectiveHls,
                    ThumbnailUrl = effectiveThumb,
                    YoutubeUrl = ytUrl,
                    IsSimulated = isSimulated,
                    StartedAt = s.StartedAt,
                    CategoryId = s.CategoryId,
                    CategoryName = s.Category?.Name,
                    CategorySlug = s.Category?.Slug,
                    IsReconnecting = s.DisconnectedAt != null,
                    ViewerCount = _viewerTracker.GetViewerCount(s.Id),
                    ProfilePictureUrl = s.Channel.ProfilePhotoUrl ?? s.Streamer.ProfilePictureUrl
                };
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

            // If the stream has already ended and already has a finalized/merged recording, don't overwrite it with a late single chunk
            if (!stream.IsLive && stream.EndedAt != null && !string.IsNullOrEmpty(stream.RecordingFileName) && stream.RecordingFileName.Contains("-merged"))
            {
                _logger.LogInformation("Stream {StreamId} already has finalized recording {FileName}. Ignoring late on_record_done.", stream.Id, stream.RecordingFileName);
                return;
            }

            stream.RecordingFileName = System.IO.Path.GetFileName(filePath);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Recording saved for stream {StreamId}: {FileName} (from NGINX webhook)",
                stream.Id, stream.RecordingFileName);
        }

        private static long? ExtractRecordingEpoch(string fileName, string streamKey)
        {
            var pattern = "^" + Regex.Escape(streamKey) + @"-(\d{9,12})";
            var match = Regex.Match(fileName, pattern);
            if (match.Success && long.TryParse(match.Groups[1].Value, out long epoch))
            {
                return epoch;
            }

            var patternDate = "^" + Regex.Escape(streamKey) + @".*?(\d{4})(\d{2})(\d{2})-(\d{2})(\d{2})(\d{2})";
            var matchDate = Regex.Match(fileName, patternDate);
            if (matchDate.Success)
            {
                try
                {
                    int y = int.Parse(matchDate.Groups[1].Value);
                    int m = int.Parse(matchDate.Groups[2].Value);
                    int d = int.Parse(matchDate.Groups[3].Value);
                    int h = int.Parse(matchDate.Groups[4].Value);
                    int min = int.Parse(matchDate.Groups[5].Value);
                    int s = int.Parse(matchDate.Groups[6].Value);
                    var dt = new DateTime(y, m, d, h, min, s, DateTimeKind.Utc);
                    return new DateTimeOffset(dt).ToUnixTimeSeconds();
                }
                catch { }
            }

            return null;
        }

        private async Task TryDropNginxPublisherAsync(string streamKey)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var baseControlUrl = _mediaServerConfig.GetControlUrl();
                var controlUrl = $"{baseControlUrl}/drop/publisher?app=live&name={Uri.EscapeDataString(streamKey)}";
                var response = await _httpClient.GetAsync(controlUrl, cts.Token);
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

        public async Task<string?> FinalizeStreamRecordingAsync(LiveStream stream, Channel channel)
        {
            if (!channel.SaveStreams || string.IsNullOrEmpty(channel.StreamKey))
            {
                return null;
            }

            try
            {
                var startedUtc = DateTime.SpecifyKind(stream.StartedAt ?? stream.CreatedAt, DateTimeKind.Utc);
                var endedUtc = DateTime.SpecifyKind(stream.EndedAt ?? DateTime.UtcNow, DateTimeKind.Utc);
                long sessionStartEpoch = new DateTimeOffset(startedUtc).ToUnixTimeSeconds();
                long sessionEndEpoch = new DateTimeOffset(endedUtc).ToUnixTimeSeconds();

                // Find candidate recording directory on local disk if available to get precise chunk list
                var candidateDirs = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "StreamingServer", "nginx", "recordings"),
                    Path.Combine(Directory.GetCurrentDirectory(), "StreamingServer", "nginx", "recordings"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StreamingServer", "nginx", "recordings")
                };

                DirectoryInfo? recDir = null;
                foreach (var dir in candidateDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        recDir = new DirectoryInfo(dir);
                        break;
                    }
                }

                List<FileInfo>? sessionChunks = null;
                if (recDir != null)
                {
                    sessionChunks = recDir.GetFiles($"{channel.StreamKey}*.flv")
                        .Concat(recDir.GetFiles($"{channel.StreamKey}*.mp4"))
                        .Where(f => f.Length > 0 && !f.Name.Contains("-merged"))
                        .Select(f => new
                        {
                            File = f,
                            Epoch = ExtractRecordingEpoch(f.Name, channel.StreamKey) ?? new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds()
                        })
                        .Where(x => x.Epoch >= (sessionStartEpoch - 300) && x.Epoch <= (sessionEndEpoch + 300))
                        .OrderBy(x => x.Epoch)
                        .Select(x => x.File)
                        .ToList();
                }

                // 1. Attempt to call media server /api/clip/merge
                var mergeUrl = _mediaServerConfig.GetClipServiceUrl().Replace("/api/clip", "/api/clip/merge");
                var payload = new
                {
                    streamKey = channel.StreamKey,
                    fileNames = sessionChunks != null && sessionChunks.Count > 0 ? sessionChunks.Select(c => c.Name).ToList() : null,
                    startTime = startedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    endTime = endedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    startEpoch = sessionStartEpoch,
                    endEpoch = sessionEndEpoch,
                    deleteSources = true
                };

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                    var response = await _httpClient.PostAsJsonAsync(mergeUrl, payload, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<MediaServerMergeResponse>();
                        if (result != null && result.Success && !string.IsNullOrEmpty(result.MergedFileName))
                        {
                            stream.RecordingFileName = result.MergedFileName;
                            _logger.LogInformation("FinalizeStreamRecording: Media server merged recording for stream {StreamId}: {FileName} (chunks: {Count})",
                                stream.Id, stream.RecordingFileName, result.FileCount);
                            return stream.RecordingFileName;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("FinalizeStreamRecording: Media server merge returned status {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FinalizeStreamRecording: Could not reach media server merge endpoint at {Url}", mergeUrl);
                }

                // 2. Fallback: Local disk scan and concatenation
                if (recDir != null && sessionChunks != null)
                {
                    if (sessionChunks.Count == 1)
                    {
                        stream.RecordingFileName = sessionChunks[0].Name;
                        _logger.LogInformation("FinalizeStreamRecording: Single recording chunk found on disk for stream {StreamId}: {FileName}", stream.Id, stream.RecordingFileName);
                        return stream.RecordingFileName;
                    }

                    if (sessionChunks.Count >= 2)
                    {
                        var ffmpegPath = FindLocalFfmpeg();
                        if (!string.IsNullOrEmpty(ffmpegPath))
                        {
                            var outName = $"{Path.GetFileNameWithoutExtension(sessionChunks[0].Name)}-merged.mp4";
                            var outPath = Path.Combine(recDir.FullName, outName);
                            var manifestFileName = $"concat_{stream.Id}_{Guid.NewGuid():N}.txt";
                            var manifestPath = Path.Combine(recDir.FullName, manifestFileName);

                            try
                            {
                                // Write relative basenames without BOM to avoid path encoding issues
                                var manifestLines = sessionChunks.Select(c => $"file '{c.Name}'");
                                var utf8NoBom = new UTF8Encoding(false);
                                await File.WriteAllLinesAsync(manifestPath, manifestLines, utf8NoBom);

                                var psi = new ProcessStartInfo
                                {
                                    FileName = ffmpegPath,
                                    Arguments = $"-y -f concat -safe 0 -i \"{manifestFileName}\" -c copy \"{outName}\"",
                                    WorkingDirectory = recDir.FullName,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };

                                using var proc = Process.Start(psi);
                                if (proc != null)
                                {
                                    await proc.WaitForExitAsync();
                                    if (proc.ExitCode == 0 && File.Exists(outPath) && new FileInfo(outPath).Length > 0)
                                    {
                                        foreach (var chunk in sessionChunks)
                                        {
                                            try { chunk.Delete(); } catch { }
                                        }

                                        stream.RecordingFileName = outName;
                                        _logger.LogInformation("FinalizeStreamRecording: Locally merged {Count} chunks into {Merged} for stream {StreamId}", sessionChunks.Count, outName, stream.Id);
                                        return stream.RecordingFileName;
                                    }
                                }
                            }
                            catch (Exception concatEx)
                            {
                                _logger.LogWarning(concatEx, "FinalizeStreamRecording: Local ffmpeg concat failed.");
                            }
                            finally
                            {
                                try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { }
                            }
                        }

                        // If concat failed, pick the latest chunk
                        stream.RecordingFileName = sessionChunks.Last().Name;
                        return stream.RecordingFileName;
                    }
                }

                // 3. Fallback to TryFindLatestRecordingOnDisk (strictly within session bounds if possible)
                var diskFile = TryFindLatestRecordingOnDisk(channel.StreamKey, sessionStartEpoch, sessionEndEpoch);
                if (!string.IsNullOrEmpty(diskFile))
                {
                    stream.RecordingFileName = diskFile;
                    return stream.RecordingFileName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinalizeStreamRecording failed for stream {StreamId}", stream.Id);
            }

            if (!string.IsNullOrEmpty(stream.RecordingFileName))
            {
                TryGenerateRecordingThumbnail(stream.RecordingFileName, null);
                stream.ThumbnailUrl = $"{_mediaServerConfig.GetRecordingsBaseUrl()}/{Path.GetFileNameWithoutExtension(stream.RecordingFileName)}.jpg";
            }

            return stream.RecordingFileName;
        }

        private void TryGenerateRecordingThumbnail(string recordingFileName, string? recDirPath)
        {
            try
            {
                var dir = recDirPath;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    var candidateDirs = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "StreamingServer", "nginx", "recordings"),
                        Path.Combine(Directory.GetCurrentDirectory(), "StreamingServer", "nginx", "recordings"),
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StreamingServer", "nginx", "recordings")
                    };
                    dir = candidateDirs.FirstOrDefault(Directory.Exists);
                }

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                var videoPath = Path.Combine(dir, recordingFileName);
                if (!File.Exists(videoPath)) return;

                var thumbPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(recordingFileName)}.jpg");
                if (File.Exists(thumbPath)) return;

                var ffmpeg = FindLocalFfmpeg() ?? "ffmpeg";
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-y -ss 00:00:02 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TryGenerateRecordingThumbnail failed for {FileName}", recordingFileName);
            }
        }

        private string? FindLocalFfmpeg()
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "..", "StreamingServer", "bin", "ffmpeg.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "StreamingServer", "bin", "ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StreamingServer", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages", "Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe", "ffmpeg-9.0.1-full_build", "bin", "ffmpeg.exe")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return null;
        }

        private string? TryFindLatestRecordingOnDisk(string streamKey, long? startEpoch = null, long? endEpoch = null)
        {
            try
            {
                var candidateDirs = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "StreamingServer", "nginx", "recordings"),
                    Path.Combine(Directory.GetCurrentDirectory(), "StreamingServer", "nginx", "recordings"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StreamingServer", "nginx", "recordings")
                };

                foreach (var dir in candidateDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var query = dirInfo.GetFiles($"{streamKey}*.flv")
                            .Concat(dirInfo.GetFiles($"{streamKey}*.mp4"))
                            .Where(f => f.Length > 0 && !f.Name.Contains("-merged"));

                        if (startEpoch.HasValue && endEpoch.HasValue)
                        {
                            query = query.Where(f =>
                            {
                                var ep = ExtractRecordingEpoch(f.Name, streamKey) ?? new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds();
                                return ep >= (startEpoch.Value - 60) && ep <= (endEpoch.Value + 60);
                            });
                        }

                        var latestFile = query.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                        if (latestFile != null)
                        {
                            _logger.LogInformation("Found recording on disk for {StreamKey}: {FileName}", streamKey, latestFile.Name);
                            return latestFile.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning recordings directory for stream key {StreamKey}", streamKey);
            }

            return null;
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

        private static (string? youtubeUrl, string? videoId, string cleanDescription) ParseYoutubeSimulated(string? description)
        {
            if (string.IsNullOrEmpty(description)) return (null, null, string.Empty);
            var match = Regex.Match(description, @"\[YOUTUBE_SIMULATED:(.*?)\]");
            if (!match.Success) return (null, null, description);

            var url = match.Groups[1].Value.Trim();
            var clean = description.Replace(match.Value, "").Trim();

            string? videoId = null;
            var idMatch = Regex.Match(url, @"(?:v=|\/live\/|\/embed\/|youtu\.be\/|\/v\/)([^?&/]+)");
            if (idMatch.Success)
            {
                videoId = idMatch.Groups[1].Value;
            }

            return (url, videoId, clean);
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

            var (ytUrl, ytId, cleanDesc) = ParseYoutubeSimulated(stream.Description);
            var isSimulated = !string.IsNullOrEmpty(ytUrl);
            var effectiveHls = isSimulated ? ytUrl : (stream.IsLive ? $"{hlsBaseUrl}/{channel.StreamKey}.m3u8" : null);
            var effectiveThumb = isSimulated && !string.IsNullOrEmpty(ytId)
                ? $"https://img.youtube.com/vi/{ytId}/hqdefault.jpg"
                : (stream.ThumbnailUrl ?? (stream.IsLive ? $"{hlsBaseUrl}/{channel.StreamKey}-preview.jpg" : null));

            return new StreamResponseDto
            {
                Id = stream.Id,
                Title = stream.Title,
                Description = cleanDesc,
                IsLive = stream.IsLive,
                HlsUrl = effectiveHls,
                ThumbnailUrl = effectiveThumb,
                YoutubeUrl = ytUrl,
                IsSimulated = isSimulated,
                VodUrl = vodUrl,
                DisconnectedAt = stream.DisconnectedAt,
                ViewerCount = _viewerTracker.GetViewerCount(stream.Id),
                StreamerId = stream.StreamerId,
                StreamerName = !string.IsNullOrEmpty(channel?.ChannelName) ? channel.ChannelName : streamer.FullName,
                ChannelName = channel?.ChannelName,
                ChannelId = channel.Id,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                CreatedAt = stream.CreatedAt,
                CategoryId = stream.CategoryId,
                CategoryName = stream.Category?.Name,
                CategorySlug = stream.Category?.Slug,
                ProfilePictureUrl = channel.ProfilePhotoUrl ?? streamer.ProfilePictureUrl
            };
        }
    }
}
