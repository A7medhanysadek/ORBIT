using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Chat;
using OrbitBackend.DTOs.Vod;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class VodService : IVodService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly ILogger<VodService> _logger;

        public VodService(
            AppDbContext context,
            IConfiguration config,
            IMediaServerConfigService mediaServerConfig,
            ILogger<VodService> logger)
        {
            _context = context;
            _config = config;
            _mediaServerConfig = mediaServerConfig;
            _logger = logger;
        }

        public async Task<List<SavedLiveDto>> GetChannelVodsAsync(int channelId)
        {
            var channel = await _context.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == channelId)
                ?? throw new InvalidOperationException("Channel not found.");

            var recordingsBaseUrl = _mediaServerConfig.GetRecordingsBaseUrl();

            // Returns all finished streams that have a recording file OR where the channel saved streams
            var rawVods = await _context.LiveStreams
                .AsNoTracking()
                .Where(s => s.ChannelId == channelId
                    && !s.IsLive
                    && s.EndedAt != null
                    && (s.RecordingFileName != null || s.Channel.SaveStreams))
                .Include(s => s.Category)
                .Include(s => s.Channel)
                .Include(s => s.VodViews)
                .Include(s => s.ChatMessages)
                .OrderByDescending(s => s.EndedAt)
                .ToListAsync();

            var vods = rawVods.Select(s =>
            {
                var fileName = ResolveVodFileName(s.RecordingFileName, s.Channel?.StreamKey);
                var thumbName = !string.IsNullOrEmpty(fileName) ? $"{Path.GetFileNameWithoutExtension(fileName)}.jpg" : null;
                var resolvedThumbnailUrl = !string.IsNullOrEmpty(s.ThumbnailUrl) && !s.ThumbnailUrl.Contains("localhost")
                    ? s.ThumbnailUrl
                    : (!string.IsNullOrEmpty(thumbName) ? $"{recordingsBaseUrl}/{thumbName}" : null);

                return new SavedLiveDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    ThumbnailUrl = resolvedThumbnailUrl,
                    VodUrl = !string.IsNullOrEmpty(fileName) ? $"{recordingsBaseUrl}/{fileName}" : null,
                    CategoryName = s.Category != null ? s.Category.Name : null,
                    CategorySlug = s.Category != null ? s.Category.Slug : null,
                    DurationSeconds = s.StartedAt.HasValue && s.EndedAt.HasValue
                        ? (s.EndedAt.Value - s.StartedAt.Value).TotalSeconds
                        : null,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    RewatchCount = s.VodViews.Count,
                    ChatMessageCount = s.ChatMessages.Count(m => !m.IsDeleted)
                };
            }).Where(v => !string.IsNullOrEmpty(v.VodUrl)).ToList();

            return vods;
        }

        public async Task<VodDetailDto> GetVodWithChatAsync(int vodId)
        {
            var recordingsBaseUrl = _mediaServerConfig.GetRecordingsBaseUrl();

            var stream = await _context.LiveStreams
                .AsNoTracking()
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .Include(s => s.Category)
                .Include(s => s.ChatMessages.Where(m => !m.IsDeleted))
                .Include(s => s.VodViews)
                .FirstOrDefaultAsync(s => s.Id == vodId)
                ?? throw new InvalidOperationException("VOD not found.");

            if (stream.IsLive)
                throw new InvalidOperationException("This stream is currently live.");

            var fileName = ResolveVodFileName(stream.RecordingFileName, stream.Channel?.StreamKey);
            if (string.IsNullOrEmpty(fileName))
                throw new InvalidOperationException("Recording file not found for this VOD.");

            var thumbName = $"{Path.GetFileNameWithoutExtension(fileName)}.jpg";
            var resolvedThumbnailUrl = !string.IsNullOrEmpty(stream.ThumbnailUrl) && !stream.ThumbnailUrl.Contains("localhost")
                ? stream.ThumbnailUrl
                : $"{recordingsBaseUrl}/{thumbName}";

            return new VodDetailDto
            {
                Id = stream.Id,
                Title = stream.Title,
                Description = stream.Description,
                ThumbnailUrl = resolvedThumbnailUrl,
                VodUrl = $"{recordingsBaseUrl}/{fileName}",
                CategoryName = stream.Category?.Name,
                CategorySlug = stream.Category?.Slug,
                DurationSeconds = stream.StartedAt.HasValue && stream.EndedAt.HasValue
                    ? (stream.EndedAt.Value - stream.StartedAt.Value).TotalSeconds
                    : null,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                RewatchCount = stream.VodViews.Count,
                StreamerName = stream.Streamer?.FullName ?? string.Empty,
                ChannelName = stream.Channel?.ChannelName ?? string.Empty,
                ChannelId = stream.ChannelId,
                ChatMessages = stream.ChatMessages
                    .OrderBy(m => m.StreamOffsetSeconds)
                    .Select(m => new ChatMessageDto
                    {
                        Id = m.Id,
                        SenderName = m.SenderName,
                        SenderBadge = m.SenderBadge,
                        Content = m.Content,
                        SentAt = m.SentAt,
                        StreamOffsetSeconds = m.StreamOffsetSeconds
                    })
                    .ToList()
            };
        }

        public async Task RecordVodViewAsync(int vodId, string? userId, string? sessionId)
        {
            var exists = await _context.LiveStreams
                .Include(s => s.Channel)
                .AnyAsync(s => s.Id == vodId && !s.IsLive && (s.RecordingFileName != null || s.Channel.SaveStreams));

            if (!exists)
                throw new InvalidOperationException("VOD not found.");

            if (!string.IsNullOrEmpty(userId))
            {
                var alreadyViewed = await _context.VodViews
                    .AnyAsync(v => v.LiveStreamId == vodId && v.UserId == userId);
                if (alreadyViewed) return;
            }
            else if (!string.IsNullOrEmpty(sessionId))
            {
                var alreadyViewed = await _context.VodViews
                    .AnyAsync(v => v.LiveStreamId == vodId && v.SessionId == sessionId);
                if (alreadyViewed) return;
            }
            else
            {
                return;
            }

            var vodView = new VodView
            {
                LiveStreamId = vodId,
                UserId = userId,
                SessionId = sessionId,
                ViewedAt = DateTime.UtcNow
            };

            _context.VodViews.Add(vodView);
            await _context.SaveChangesAsync();

            _logger.LogDebug("VOD view recorded for stream {VodId}.", vodId);
        }

        public async Task DeleteVodAsync(int vodId, string userId)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Channel)
                .FirstOrDefaultAsync(s => s.Id == vodId)
                ?? throw new InvalidOperationException("VOD not found.");

            if (stream.Channel.OwnerId != userId)
                throw new UnauthorizedAccessException("You can only delete VODs from your own channel.");

            stream.RecordingFileName = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("VOD for stream {StreamId} deleted by channel owner {UserId}.", vodId, userId);
        }

        private string? ResolveVodFileName(string? recordingFileName, string? streamKey)
        {
            if (!string.IsNullOrEmpty(recordingFileName))
                return recordingFileName;

            if (string.IsNullOrEmpty(streamKey))
                return null;

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
                        var latestFile = dirInfo.GetFiles($"{streamKey}*.flv")
                            .Concat(dirInfo.GetFiles($"{streamKey}*.mp4"))
                            .Where(f => f.Length > 0)
                            .OrderByDescending(f => f.LastWriteTimeUtc)
                            .FirstOrDefault();

                        if (latestFile != null)
                        {
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
    }
}
