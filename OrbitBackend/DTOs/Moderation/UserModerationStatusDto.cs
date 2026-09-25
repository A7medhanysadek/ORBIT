namespace OrbitBackend.DTOs.Moderation
{
    public class UserModerationStatusDto
    {
        public string Username { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
        public bool IsModerator { get; set; }
        public bool IsTimedOut { get; set; }
        public bool IsBanned { get; set; }
        public int TimeoutRemainingSeconds { get; set; }
    }
}
