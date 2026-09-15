namespace OrbitBackend.DTOs.Admin
{
    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalChannels { get; set; }
        public int ActiveStreams { get; set; }
        public int TotalClips { get; set; }
        public int TotalVods { get; set; }
        public int TotalCategories { get; set; }
        public int TotalChatMessages { get; set; }
    }

    public class AdminUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool HasChannel { get; set; }
        public int? ChannelId { get; set; }
        public string? ChannelName { get; set; }
    }

    public class UpdateUserRolesDto
    {
        public List<string> Roles { get; set; } = new();
    }

    public class LockUserDto
    {
        public bool IsLocked { get; set; }
        public int LockoutMinutes { get; set; } = 1440; // Default 24 hours
    }

    public class AdminResetPasswordDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AdminChannelDto
    {
        public int Id { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerUsername { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public bool HasStreamKey { get; set; }
        public bool IsLive { get; set; }
        public int CurrentViewers { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminStreamDto
    {
        public int StreamId { get; set; }
        public int ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string StreamerName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int ViewerCount { get; set; }
        public DateTime StartedAt { get; set; }
        public string? CategoryName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool IsSimulated { get; set; }
        public string? YoutubeUrl { get; set; }
    }

    public class ChannelSearchResultDto
    {
        public int Id { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
        public bool IsLive { get; set; }
        public int ViewerCount { get; set; }
        public string? CategoryName { get; set; }
    }

    public class SimulateYoutubeStreamDto
    {
        public int ChannelId { get; set; }
        public string YoutubeUrl { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int? CategoryId { get; set; }
    }
}
