using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Clip
{
    /// <summary>
    /// Request DTO for creating a live clip from an active or recent stream.
    /// Sliced on the Media Server side (default 60s, max 300s / 5 min).
    /// </summary>
    public class SliceClipDto
    {
        /// <summary>
        /// ID of the live stream to clip from.
        /// </summary>
        public int? LiveStreamId { get; set; }

        /// <summary>
        /// ID of the channel to clip from (if LiveStreamId is omitted, uses active stream).
        /// </summary>
        public int? ChannelId { get; set; }

        /// <summary>
        /// Title of the clip.
        /// </summary>
        [Required(ErrorMessage = "Clip title is required.")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Clip title must be between 1 and 150 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Duration of the clip in seconds. Defaults to 60 (1 minute). Maximum is 300 (5 minutes). Minimum is 5 seconds.
        /// </summary>
        [Range(5, 300, ErrorMessage = "Clip duration must be between 5 and 300 seconds (5 minutes maximum).")]
        public int? DurationSeconds { get; set; } = 60;
    }
}
