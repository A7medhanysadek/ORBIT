using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Moderation
{
    public class TimeoutUserDto
    {
        /// <summary>
        /// Username of the user to time out.
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Duration in seconds. Must respect the configured min/max bounds.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int DurationSeconds { get; set; }

        /// <summary>
        /// Optional reason for the timeout.
        /// </summary>
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
