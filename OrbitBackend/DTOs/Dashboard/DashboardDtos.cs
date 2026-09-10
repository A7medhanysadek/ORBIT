namespace OrbitBackend.DTOs.Dashboard
{
    /// <summary>
    /// Root overview DTO for the Streamer Dashboard / Creator Studio.
    /// Combines channel information, current live status, and lifetime aggregated metrics.
    /// </summary>
    public class StreamerDashboardSummaryDto
    {
        // ── Channel Profile ──
        public int ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public bool SaveStreams { get; set; }
        public string? DonationUrl { get; set; }
        public string? DonationMessage { get; set; }

        // ── Live Status (null if streamer is offline) ──
        public CurrentStreamStatusDto? LiveManager { get; set; }

        // ── Lifetime Performance & KPI Cards ──
        public DashboardLifetimeStatsDto LifetimeStats { get; set; } = new();
    }

    /// <summary>
    /// Aggregated lifetime metrics for the streamer's channel.
    /// </summary>
    public class DashboardLifetimeStatsDto
    {
        public int TotalStreams { get; set; }
        public double TotalBroadcastSeconds { get; set; }
        public string TotalBroadcastHours { get; set; } = "0h";
        public double AverageStreamDurationSeconds { get; set; }
        public string AverageStreamDurationFormatted { get; set; } = "0m";

        public int AllTimePeakViewers { get; set; }
        public double AveragePeakViewers { get; set; }

        public int TotalVodViews { get; set; }
        public int TotalClipViews { get; set; }
        public int TotalClipsCount { get; set; }

        public int TotalChatMessages { get; set; }
        public int UniqueChattersCount { get; set; }

        public int TotalModerators { get; set; }
        public int TotalBannedUsers { get; set; }
    }

    /// <summary>
    /// Real-time live session controls and metrics for the streamer's active stream.
    /// </summary>
    public class CurrentStreamStatusDto
    {
        public int StreamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Category
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public string? CategoryImageUrl { get; set; }

        // Status & Timer
        public bool IsLive { get; set; }
        public bool IsReconnecting { get; set; }
        public DateTime? StartedAt { get; set; }
        public double UptimeSeconds { get; set; }
        public string FormattedUptime { get; set; } = "00:00:00";

        // Viewers
        public int CurrentViewerCount { get; set; }
        public int SessionPeakViewers { get; set; }

        // Ingest & Playback Credentials
        public string StreamKeyMasked { get; set; } = string.Empty;
        public string? StreamKeyFull { get; set; }
        public string RtmpIngestUrl { get; set; } = string.Empty;
        public string HlsPlaybackUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Per-stream history item for the Past Broadcasts / Stream History panel.
    /// </summary>
    public class PastStreamDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public string? CategoryImageUrl { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public double DurationSeconds { get; set; }
        public string FormattedDuration { get; set; } = "0m";

        public int PeakViewers { get; set; }
        public int RewatchCount { get; set; }
        public int ChatMessageCount { get; set; }
        public int ClipsCount { get; set; }

        public bool IsSaved { get; set; }
        public string? VodUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// Recap returned when the streamer ends a broadcast session.
    /// </summary>
    public class StreamSessionSummaryDto
    {
        public int StreamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public double DurationSeconds { get; set; }
        public string FormattedDuration { get; set; } = "0m";

        public int PeakViewers { get; set; }
        public int TotalChatMessages { get; set; }
        public bool IsSavedAsVod { get; set; }
        public string? VodUrl { get; set; }
        public string Message { get; set; } = "Stream ended successfully.";
    }

    /// <summary>
    /// Moderation hub summary for the streamer's channel.
    /// </summary>
    public class DashboardModerationSummaryDto
    {
        public int TotalModerators { get; set; }
        public List<DashboardModeratorDto> Moderators { get; set; } = new();

        public int ActiveBansCount { get; set; }
        public List<DashboardBannedUserDto> BannedUsers { get; set; } = new();

        public int ActiveTimeoutsCount { get; set; }
        public List<DashboardTimeoutUserDto> TimedOutUsers { get; set; } = new();
    }

    public class DashboardModeratorDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class DashboardBannedUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public string? Reason { get; set; }
        public DateTime BannedAt { get; set; }
        public string BannedBy { get; set; } = string.Empty;
    }

    public class DashboardTimeoutUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public string? Reason { get; set; }
        public DateTime TimedOutAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int RemainingSeconds { get; set; }
        public string TimedOutBy { get; set; } = string.Empty;
    }
}
