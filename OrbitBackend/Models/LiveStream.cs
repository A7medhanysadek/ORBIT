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

        /// <summary>
        /// File name of the recorded stream (set by nginx on_record_done callback).
        /// Null if no recording exists.
        /// </summary>
        public string? RecordingFileName { get; set; }

        
        public string StreamerId { get; set; } = string.Empty;
        public AppUser Streamer { get; set; } = null!;
        public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}
