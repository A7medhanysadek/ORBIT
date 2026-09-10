using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Clip
{
    /// <summary>
    /// Request DTO for creating a new highlight clip.
    /// Can be submitted as multipart/form-data (with video file) or JSON (with VideoUrl).
    /// </summary>
    public class CreateClipDto
    {
        [Required(ErrorMessage = "Clip title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Channel ID is required.")]
        public int ChannelId { get; set; }

        /// <summary>
        /// The live stream from which this clip was taken (optional).
        /// </summary>
        public int? LiveStreamId { get; set; }

        /// <summary>
        /// Optional category ID. If not provided and LiveStreamId is present,
        /// it automatically inherits the stream's category.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Approximate duration of the clip in seconds (typically 10-60s).
        /// </summary>
        [Range(1, 300, ErrorMessage = "Duration must be between 1 and 300 seconds.")]
        public double DurationSeconds { get; set; } = 30;

        /// <summary>
        /// Direct URL to video (if uploaded directly or externally).
        /// Ignored if a video file is attached in the request.
        /// </summary>
        public string? VideoUrl { get; set; }

        /// <summary>
        /// Optional thumbnail image URL.
        /// </summary>
        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// Response DTO containing complete details of a clip.
    /// </summary>
    public class ClipResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public double DurationSeconds { get; set; }
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // ── Creator ──
        public string CreatorId { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string? CreatorProfilePictureUrl { get; set; }

        // ── Channel ──
        public int ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string? ChannelProfilePhotoUrl { get; set; }

        // ── Stream (if clipped from a stream) ──
        public int? LiveStreamId { get; set; }
        public string? StreamTitle { get; set; }

        // ── Category ──
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public string? CategoryImageUrl { get; set; }
    }
}
