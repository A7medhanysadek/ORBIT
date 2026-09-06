namespace OrbitBackend.DTOs.Moderation
{
    /// <summary>
    /// Response DTO returned after any moderation action.
    /// </summary>
    public class ModerationActionDto
    {
        public string Action { get; set; } = string.Empty;
        public string TargetUsername { get; set; } = string.Empty;
        public string ModeratorName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public int? DurationSeconds { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
    }
}
