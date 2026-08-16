namespace OrbitBackend.Models
{
    public class LiveStream
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsLive { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        
        public string StreamerId { get; set; } = string.Empty;
        public AppUser Streamer { get; set; } = null!;
    }
}
