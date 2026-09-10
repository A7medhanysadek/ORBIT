using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Dashboard;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;
        private readonly IStreamService _streamService;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly ViewerTracker _viewerTracker;
        private readonly IConfiguration _config;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            AppDbContext context,
            IStreamService streamService,
            IMediaServerConfigService mediaServerConfig,
            ViewerTracker viewerTracker,
            IConfiguration config,
            ILogger<DashboardService> logger)
        {
            _context = context;
            _streamService = streamService;
            _mediaServerConfig = mediaServerConfig;
            _viewerTracker = viewerTracker;
            _config = config;
            _logger = logger;
        }

        public async Task<StreamerDashboardSummaryDto> GetDashboardSummaryAsync(string userId)
        {
            var channel = await GetUserChannelAsync(userId);

            // 1. Live Manager status
            var liveManager = await GetLiveManagerStatusAsync(userId);

            // 2. Lifetime statistics
            var allStreams = await _context.LiveStreams
                .AsNoTracking()
                .Where(s => s.ChannelId == channel.Id)
                .Include(s => s.VodViews)
                .Include(s => s.ChatMessages)
                .ToListAsync();

            var finishedStreams = allStreams
                .Where(s => s.StartedAt.HasValue && s.EndedAt.HasValue)
                .ToList();

            double totalBroadcastSeconds = finishedStreams.Sum(s => (s.EndedAt!.Value - s.StartedAt!.Value).TotalSeconds);
            double avgDuration = finishedStreams.Count > 0 ? totalBroadcastSeconds / finishedStreams.Count : 0;
            int allTimePeak = allStreams.Count > 0 ? allStreams.Max(s => s.PeakViewers) : 0;
            double avgPeak = allStreams.Count > 0 ? allStreams.Average(s => s.PeakViewers) : 0;
            int totalVodViews = allStreams.Sum(s => s.VodViews.Count);
            int totalChatMessages = allStreams.Sum(s => s.ChatMessages.Count);

            int totalClipsCount = await _context.Clips
                .CountAsync(c => c.ChannelId == channel.Id);

            int totalClipViews = await _context.Clips
                .Where(c => c.ChannelId == channel.Id)
                .SumAsync(c => c.ViewCount);

            int uniqueChattersCount = await _context.ChatMessages
                .Where(m => m.LiveStream.ChannelId == channel.Id)
                .Select(m => m.SenderId)
                .Distinct()
                .CountAsync();

            int totalModerators = await _context.ChannelModerators
                .CountAsync(m => m.ChannelId == channel.Id);

            int totalBannedUsers = await _context.ChatBans
                .CountAsync(b => b.ChannelId == channel.Id && b.IsActive);

            var lifetimeStats = new DashboardLifetimeStatsDto
            {
                TotalStreams = allStreams.Count,
                TotalBroadcastSeconds = totalBroadcastSeconds,
                TotalBroadcastHours = $"{totalBroadcastSeconds / 3600.0:0.1} hrs",
                AverageStreamDurationSeconds = avgDuration,
                AverageStreamDurationFormatted = FormatDuration(avgDuration),
                AllTimePeakViewers = allTimePeak,
                AveragePeakViewers = Math.Round(avgPeak, 1),
                TotalVodViews = totalVodViews,
                TotalClipViews = totalClipViews,
                TotalClipsCount = totalClipsCount,
                TotalChatMessages = totalChatMessages,
                UniqueChattersCount = uniqueChattersCount,
                TotalModerators = totalModerators,
                TotalBannedUsers = totalBannedUsers
            };

            return new StreamerDashboardSummaryDto
            {
                ChannelId = channel.Id,
                ChannelName = channel.ChannelName,
                Description = channel.Description,
                ProfilePhotoUrl = channel.ProfilePhotoUrl,
                CoverPhotoUrl = channel.CoverPhotoUrl,
                SaveStreams = channel.SaveStreams,
                DonationUrl = channel.DonationUrl,
                DonationMessage = channel.DonationMessage,
                LiveManager = liveManager,
                LifetimeStats = lifetimeStats
            };
        }

        public async Task<CurrentStreamStatusDto?> GetLiveManagerStatusAsync(string userId)
        {
            var channel = await GetUserChannelAsync(userId);

            var stream = await _context.LiveStreams
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => s.StreamerId == userId && (s.IsLive || s.EndedAt == null))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (stream == null)
                return null;

            var now = DateTime.UtcNow;
            var uptimeSeconds = stream.StartedAt.HasValue ? (now - stream.StartedAt.Value).TotalSeconds : 0;
            var currentViewers = _viewerTracker.GetViewerCount(stream.Id);
            var peakViewers = Math.Max(stream.PeakViewers, _viewerTracker.GetPeakViewerCount(stream.Id));

            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();
            var rtmpUrl = _mediaServerConfig.GetRtmpUrl();

            return new CurrentStreamStatusDto
            {
                StreamId = stream.Id,
                Title = stream.Title,
                Description = stream.Description,
                CategoryId = stream.CategoryId,
                CategoryName = stream.Category?.Name,
                CategorySlug = stream.Category?.Slug,
                CategoryImageUrl = stream.Category?.ImageUrl,
                IsLive = stream.IsLive,
                IsReconnecting = stream.DisconnectedAt != null,
                StartedAt = stream.StartedAt,
                UptimeSeconds = uptimeSeconds,
                FormattedUptime = FormatDuration(uptimeSeconds),
                CurrentViewerCount = currentViewers,
                SessionPeakViewers = peakViewers,
                StreamKeyMasked = MaskStreamKey(channel.StreamKey),
                StreamKeyFull = channel.StreamKey,
                RtmpIngestUrl = rtmpUrl,
                HlsPlaybackUrl = $"{hlsBaseUrl}/{channel.StreamKey}.m3u8"
            };
        }

        public async Task<List<PastStreamDto>> GetPastStreamsAsync(string userId, int page = 1, int pageSize = 10)
        {
            var channel = await GetUserChannelAsync(userId);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();
            var recordingsBaseUrl = _config["Streaming:RecordingsBaseUrl"] ?? hlsBaseUrl.Replace("/hls", "/recordings");

            var streams = await _context.LiveStreams
                .AsNoTracking()
                .Where(s => s.ChannelId == channel.Id && !s.IsLive && s.EndedAt != null)
                .Include(s => s.Category)
                .Include(s => s.ChatMessages)
                .Include(s => s.VodViews)
                .Include(s => s.Clips)
                .OrderByDescending(s => s.EndedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return streams.Select(s =>
            {
                var duration = s.StartedAt.HasValue && s.EndedAt.HasValue
                    ? (s.EndedAt.Value - s.StartedAt.Value).TotalSeconds
                    : 0;

                var fileName = s.RecordingFileName ?? (channel.SaveStreams ? $"{channel.StreamKey}.flv" : null);

                return new PastStreamDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    CategoryId = s.CategoryId,
                    CategoryName = s.Category?.Name,
                    CategorySlug = s.Category?.Slug,
                    CategoryImageUrl = s.Category?.ImageUrl,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    DurationSeconds = duration,
                    FormattedDuration = FormatDuration(duration),
                    PeakViewers = s.PeakViewers,
                    RewatchCount = s.VodViews.Count,
                    ChatMessageCount = s.ChatMessages.Count(m => !m.IsDeleted),
                    ClipsCount = s.Clips.Count,
                    IsSaved = !string.IsNullOrEmpty(fileName),
                    VodUrl = !string.IsNullOrEmpty(fileName) ? $"{recordingsBaseUrl}/{fileName}" : null,
                    ThumbnailUrl = s.ThumbnailUrl
                };
            }).ToList();
        }

        public async Task<DashboardModerationSummaryDto> GetModerationSummaryAsync(string userId)
        {
            var channel = await GetUserChannelAsync(userId);
            var now = DateTime.UtcNow;

            var moderators = await _context.ChannelModerators
                .AsNoTracking()
                .Where(m => m.ChannelId == channel.Id)
                .Include(m => m.User)
                .OrderBy(m => m.HiredAt)
                .Select(m => new DashboardModeratorDto
                {
                    UserId = m.UserId,
                    FullName = m.User.FullName,
                    Username = m.User.UserName ?? string.Empty,
                    ProfilePictureUrl = m.User.ProfilePictureUrl,
                    AssignedAt = m.HiredAt
                })
                .ToListAsync();

            var bans = await _context.ChatBans
                .AsNoTracking()
                .Where(b => b.ChannelId == channel.Id && b.IsActive)
                .Include(b => b.User)
                .Include(b => b.Moderator)
                .OrderByDescending(b => b.BannedAt)
                .Select(b => new DashboardBannedUserDto
                {
                    UserId = b.UserId,
                    Username = b.User.UserName ?? string.Empty,
                    FullName = b.User.FullName,
                    ProfilePictureUrl = b.User.ProfilePictureUrl,
                    Reason = b.Reason,
                    BannedAt = b.BannedAt,
                    BannedBy = b.Moderator.FullName
                })
                .ToListAsync();

            var timeouts = await _context.ChatTimeouts
                .AsNoTracking()
                .Where(t => t.ChannelId == channel.Id && t.ExpiresAt > now)
                .Include(t => t.User)
                .Include(t => t.Moderator)
                .OrderByDescending(t => t.IssuedAt)
                .Select(t => new DashboardTimeoutUserDto
                {
                    UserId = t.UserId,
                    Username = t.User.UserName ?? string.Empty,
                    FullName = t.User.FullName,
                    ProfilePictureUrl = t.User.ProfilePictureUrl,
                    Reason = t.Reason,
                    TimedOutAt = t.IssuedAt,
                    ExpiresAt = t.ExpiresAt,
                    RemainingSeconds = (int)Math.Max(0, (t.ExpiresAt - now).TotalSeconds),
                    TimedOutBy = t.Moderator.FullName
                })
                .ToListAsync();

            return new DashboardModerationSummaryDto
            {
                TotalModerators = moderators.Count,
                Moderators = moderators,
                ActiveBansCount = bans.Count,
                BannedUsers = bans,
                ActiveTimeoutsCount = timeouts.Count,
                TimedOutUsers = timeouts
            };
        }

        public async Task<StreamSessionSummaryDto> EndCurrentStreamAsync(string userId)
        {
            return await _streamService.EndStreamAsync(userId);
        }

        public async Task<StreamResponseDto> UpdateCurrentStreamMetadataAsync(string userId, UpdateLiveStreamDto dto)
        {
            return await _streamService.UpdateStreamAsync(userId, dto);
        }

        public async Task<StreamKeyResponseDto> RegenerateStreamKeyAsync(string userId)
        {
            return await _streamService.GenerateStreamKeyAsync(userId);
        }

        private async Task<Channel> GetUserChannelAsync(string userId)
        {
            return await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You must create a channel before accessing the streamer dashboard.");
        }

        private static string MaskStreamKey(string? streamKey)
        {
            if (string.IsNullOrEmpty(streamKey)) return string.Empty;
            if (streamKey.Length <= 8) return "••••••••";
            return $"sk_live_••••••••{streamKey[^4..]}";
        }

        private static string FormatDuration(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }
}
